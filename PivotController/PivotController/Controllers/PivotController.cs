using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Syncfusion.Blazor.PivotView;

namespace PivotController.Controllers
{
    [Route("api/[controller]")]
    public class PivotController : Controller
    {
        private readonly IMemoryCache _cache;
        private IWebHostEnvironment _hostingEnvironment;
        private bool isRendered;
        private PivotEngine<DataSource.PivotViewData> PivotEngine = new PivotEngine<DataSource.PivotViewData>();

        public PivotController(IMemoryCache cache, IWebHostEnvironment environment)
        {
            _cache = cache;
            _hostingEnvironment = environment;
        }

        // POST api/<controller>
        [Route("/api/pivot/post")]
        [HttpPost]
        public async Task<object> Post([FromBody]object args)
        {
            FetchData param = JsonConvert.DeserializeObject<FetchData>(args.ToString());
            if (param.Action == "fetchFieldMembers")
            {
                return await GetMembers(param);
            }
            else if (param.Action == "fetchRawData")
            {
                return await GetRawData(param);
            }
            else
            {
                return await GetPivotValues(param);
            }
        }
        public async Task<EngineProperties> GetEngine(FetchData param)
        {
            isRendered = false;
            return await _cache.GetOrCreateAsync("engine" + param.Hash,
                async (cacheEntry) =>
                {
                    isRendered = true;
                    cacheEntry.SetSize(1);
                    cacheEntry.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(60);
                    PivotEngine.Data = await GetData(param);
                    return await PivotEngine.GetEngine(param);
                });
        }

        public async Task<object> GetData(FetchData param)
        {
            return await _cache.GetOrCreateAsync("dataSource" + param.Hash,
                async (cacheEntry) =>
                {
                    cacheEntry.SetSize(1);
                    cacheEntry.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(60);
                    // Here you need to return raw data
                    return new DataSource.PivotViewData().GetVirtualData();
                    //return new DataSource.PivotDynamicData().GetDynamicData();
                    //return new DataSource.PivotExpandoData().GetExpandoData();
                    //return new DataSource.GroupData().GetGroupData();
                    //return new DataSource.BusinessObjectsDataView().GetDataTable();
                    //return new DataSource.PivotJSONData().ReadJSONData(_hostingEnvironment.ContentRootPath + "\\DataSource\\sales-analysis.json");
                    //return new DataSource.PivotCSVData().ReadCSVData(_hostingEnvironment.ContentRootPath + "\\DataSource\\sales.csv");
                    //return new DataSource.PivotJSONData().ReadJSONData("http://cdn.syncfusion.com/data/sales-analysis.json");
                    //return new DataSource.PivotCSVData().ReadCSVData("http://cdn.syncfusion.com/data/sales-analysis.csv");
                });
        }

        public async Task<object> GetMembers(FetchData param)
        {
            EngineProperties engine = await GetEngine(param);
            Dictionary<string, object> returnValue = new Dictionary<string, object>();
            returnValue["memberName"] = param.MemberName;
            returnValue["members"] = JsonConvert.SerializeObject(engine.FieldList[param.MemberName].DateMember);
            return returnValue;
        }

        public async Task<object> GetRawData(FetchData param)
        {
            EngineProperties engine = await GetEngine(param);
            return PivotEngine.GetRawData(param, engine);
        }

        public async Task<object> GetPivotValues(FetchData param)
        {
            EngineProperties engine = await GetEngine(param);
            if (!isRendered)
            {
                engine = await PivotEngine.PerformAction(engine, param);
            }
            _cache.Remove("engine" + param.Hash);
            _cache.Set("engine" + param.Hash, engine, new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(DateTimeOffset.UtcNow.AddMinutes(60)));
            return PivotEngine.GetSerializedPivotValues();
        }

    }
}
