﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Syncfusion.Pivot.Engine;


namespace PivotController.Controllers
{
    [Route("api/[controller]")]
    public class PivotController : Controller
    {
        private readonly IMemoryCache _cache;
        private IWebHostEnvironment _hostingEnvironment;
        private bool isRendered;
        private PivotEngine<PivotViewData> PivotEngine = new PivotEngine<PivotViewData>();
        private ExcelExport excelExport = new ExcelExport();

        public PivotController(IMemoryCache cache, IWebHostEnvironment environment)
        {
            _cache = cache;
            _hostingEnvironment = environment;
        }

        [Route("/api/pivot/post")]
        [HttpPost]
        public async Task<object> Post([FromBody] object args)
        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            FetchData param = JsonConvert.DeserializeObject<FetchData>(args.ToString());
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            if (param.Action == "fetchFieldMembers")
            {
                return await GetMembers(param);
            }
            else if (param.Action == "fetchRawData")
            {
                return await GetRawData(param);
            }
            else if (param.Action == "onExcelExport" || param.Action == "onCsvExport")
            {
                EngineProperties engine = await GetEngine(param);
                if (param.InternalProperties.EnableVirtualization && param.ExportAllPages)
                {
                    engine = await PivotEngine.PerformAction(engine, param);
                }
                if (param.Action == "onExcelExport")
                {
                    return excelExport.ExportToExcel("Excel", engine, null, param.ExcelExportProperties);
                }
                else
                {
                    return excelExport.ExportToExcel("CSV", engine);
                }
            }
            else
            {
                return await GetPivotValues(param);
            }
        }

        private async Task<EngineProperties> GetEngine(FetchData param)
        {
            isRendered = false;
#pragma warning disable CS8603 // Possible null reference return.
            return await _cache.GetOrCreateAsync("engine" + param.Hash,
                async (cacheEntry) =>
                {
                    isRendered = true;
                    cacheEntry.SetSize(1);
                    cacheEntry.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(60);
                    PivotEngine.Data = await GetData(param);
                    return await PivotEngine.GetEngine(param);
                });
#pragma warning restore CS8603 // Possible null reference return.
        }

        private async Task<object> GetData(FetchData param)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return await _cache.GetOrCreateAsync("dataSource" + param.Hash,
                async (cacheEntry) =>
                {
                    cacheEntry.SetSize(1);
                    cacheEntry.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(60);

                    // Here, you can refer different kinds of data sources. We've bound a collection in this illustration.
                    // return new PivotViewData().GetVirtualData();
                    return new PivotViewData().GetVirtualData();
                });
#pragma warning restore CS8603 // Possible null reference return.
        }

        private async Task<object> GetMembers(FetchData param)
        {
            EngineProperties engine = await GetEngine(param);
            Dictionary<string, object> returnValue = new Dictionary<string, object>();
            returnValue["memberName"] = param.MemberName;
            if (engine.FieldList[param.MemberName].IsMembersFilled)
            {
                returnValue["members"] = JsonConvert.SerializeObject(engine.FieldList[param.MemberName].Members);
            }
            else
            {
                await PivotEngine.PerformAction(engine, param);
                returnValue["members"] = JsonConvert.SerializeObject(engine.FieldList[param.MemberName].Members);
            }
            return returnValue;
        }

        private async Task<object> GetRawData(FetchData param)
        {
            EngineProperties engine = await GetEngine(param);
            return PivotEngine.GetRawData(param, engine);
        }

        private async Task<object> GetPivotValues(FetchData param)
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
    public class PivotViewData
    {
        public string ProductID { get; set; }
        public string Country { get; set; }
        public string Product { get; set; }
        public double Sold { get; set; }
        public double Price { get; set; }
        public string Year { get; set; }

        public List<PivotViewData> GetVirtualData()
        {
            List<PivotViewData> VirtualData = new List<PivotViewData>();

            for (int i = 1; i <= 100; i++)
            {
                PivotViewData p = new PivotViewData
                {
                    ProductID = "PRO-" + (100 + i),
                    Year = (new string[] { "FY 2015", "FY 2016", "FY 2017", "FY 2018", "FY 2019" })[new Random().Next(5)],
                    Country = (new string[] { "Canada", "France", "Australia", "Germany", "France" })[new Random().Next(5)],
                    Product = (new string[] { "Car", "Van", "Bike", "Flight", "Bus" })[new Random().Next(5)],
                    Price = (3.4 * i) + 500,
                    Sold = (i * 15) + 10
                };
                VirtualData.Add(p);
            }
            return VirtualData;
        }
    }
}
