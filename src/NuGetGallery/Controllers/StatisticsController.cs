﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI;
using NuGet.Versioning;

namespace NuGetGallery
{
    public partial class StatisticsController
        : AppController
    {
        private readonly IStatisticsService _statisticsService = null;
        private readonly IAggregateStatsService _aggregateStatsService = null;

        private static readonly string[] PackageDownloadsByVersionDimensions = new[] {
            Constants.StatisticsDimensions.Version,
            Constants.StatisticsDimensions.ClientName,
            Constants.StatisticsDimensions.ClientVersion,
        };

        private static readonly string[] PackageDownloadsDetailDimensions = new[] {
            Constants.StatisticsDimensions.ClientName,
            Constants.StatisticsDimensions.ClientVersion,
        };

        public StatisticsController(IAggregateStatsService aggregateStatsService)
        {
            _statisticsService = null;
            _aggregateStatsService = aggregateStatsService;
        }

        public StatisticsController(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
            _aggregateStatsService = null;
        }

        public StatisticsController(IStatisticsService statisticsService, IAggregateStatsService aggregateStatsService)
        {
            _statisticsService = statisticsService;
            _aggregateStatsService = aggregateStatsService;
        }

        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Head)]
        [OutputCache(VaryByHeader = "Accept-Language", Duration = 3600, Location = OutputCacheLocation.Server)]
        public virtual async Task<ActionResult> Totals()
        {
            var stats = await _aggregateStatsService.GetAggregateStats();

            return Json(
                new
                {
                    Downloads = stats.Downloads,
                    UniquePackages = stats.UniquePackages,
                    TotalPackages = stats.TotalPackages,
                    LastUpdatedDateUtc = stats.LastUpdateDateUtc
                },
                JsonRequestBehavior.AllowGet);
        }

        //
        // GET: /stats

        public virtual async Task<ActionResult> Index()
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var availablity = await Task.WhenAll(
                _statisticsService.LoadDownloadPackages(),
                _statisticsService.LoadDownloadPackageVersions(),
                _statisticsService.LoadNuGetClientVersion(),
                _statisticsService.LoadLast6Weeks());

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageAvailable = availablity[0].Loaded,
                DownloadPackagesSummary = _statisticsService.DownloadPackagesSummary,
                IsDownloadPackageDetailAvailable = availablity[1].Loaded,
                DownloadPackageVersionsSummary = _statisticsService.DownloadPackageVersionsSummary,
                IsNuGetClientVersionAvailable = availablity[2].Loaded,
                NuGetClientVersion = _statisticsService.NuGetClientVersion,
                IsLast6WeeksAvailable = availablity[3].Loaded,
                Last6Weeks = _statisticsService.Last6Weeks,
                LastUpdatedUtc = availablity
                    .Where(r => r.LastUpdatedUtc.HasValue)
                    .OrderByDescending(r => r.LastUpdatedUtc.Value)
                    .Select(r => r.LastUpdatedUtc)
                    .FirstOrDefault()
            };

            model.Update();

            model.UseD3 = UseD3();

            return View(model);
        }

        //
        // GET: /stats/packages

        public virtual async Task<ActionResult> Packages()
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var result = await _statisticsService.LoadDownloadPackages();

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageAvailable = result.Loaded,
                DownloadPackagesAll = _statisticsService.DownloadPackagesAll,
                LastUpdatedUtc = result.LastUpdatedUtc
            };

            return View(model);
        }

        //
        // GET: /stats/packageversions

        public virtual async Task<ActionResult> PackageVersions()
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var result = await _statisticsService.LoadDownloadPackageVersions();

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageDetailAvailable = result.Loaded,
                DownloadPackageVersionsAll = _statisticsService.DownloadPackageVersionsAll,
                LastUpdatedUtc = result.LastUpdatedUtc
            };

            return View(model);
        }

        //
        // GET: /stats/packages/{id}

        public virtual async Task<ActionResult> PackageDownloadsByVersion(string id, string[] groupby)
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var report = await GetPackageDownloadsByVersionReport(id, groupby);

            var model = new StatisticsPackagesViewModel();

            model.SetPackageDownloadsByVersion(id);

            model.UseD3 = UseD3();

            return View(model);
        }

        //
        // GET: /stats/reports/packages/{id}

        public virtual async Task<ActionResult> PackageDownloadsByVersionReport(string id, string[] groupby)
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            return Json(await GetPackageDownloadsByVersionReport(id, groupby), JsonRequestBehavior.AllowGet);
        }

        //
        // GET: /stats/packages/{id}/{version}

        public virtual async Task<ActionResult> PackageDownloadsDetail(string id, string version, string[] groupby)
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var report = await GetPackageDownloadsDetailReport(id, version, groupby);

            var model = new StatisticsPackagesViewModel();

            model.SetPackageVersionDownloadsByClient(id, version);

            model.UseD3 = UseD3();

            return View(model);
        }

        //
        // GET: /stats/reports/packages/{id}/{version}

        public virtual async Task<ActionResult> PackageDownloadsDetailReport(string id, string version, string[] groupby)
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var report = await GetPackageDownloadsDetailReport(id, version, groupby);

            if (report != null)
            {
                report.Id = MakeReportId(groupby);
            }

            return Json(report, JsonRequestBehavior.AllowGet);
        }

        private async Task<StatisticsPackagesReport> GetPackageDownloadsByVersionReport(string id, string[] groupby)
        {
            StatisticsPackagesReport report = null;
            try
            {
                report = await _statisticsService.GetPackageDownloadsByVersion(id);

                ProcessReport(report, groupby, PackageDownloadsByVersionDimensions, id);
            }
            catch (StatisticsReportNotFoundException)
            {
                // no report found
            }

            if (report != null)
            {
                report.Id = MakeReportId(groupby);
            }

            return report;
        }

        private async Task<StatisticsPackagesReport> GetPackageDownloadsDetailReport(string id, string version, string[] groupby)
        {
            StatisticsPackagesReport report = null;
            try
            {
                report = await _statisticsService.GetPackageVersionDownloadsByClient(id, version);

                ProcessReport(report, groupby, PackageDownloadsDetailDimensions, null);
            }
            catch (StatisticsReportNotFoundException)
            {
                // no report found
            }

            if (report != null)
            {
                report.Id = MakeReportId(groupby);
            }

            return report;
        }

        private void ProcessReport(StatisticsPackagesReport report, string[] groupby, string[] dimensions, string id)
        {
            if (report == null)
            {
                return;
            }

            var pivot = new string[4];
            if (groupby != null)
            {
                // process and validate the groupby query. unrecognized fields are ignored. others fields regarded for existence
                var dim = 0;

                foreach (var dimension in dimensions)
                {
                    CheckGroupBy(groupby, dimension, pivot, ref dim, report);
                }

                if (dim == 0)
                {
                    // no recognized fields so just fall into the null logic
                    groupby = null;
                }
                else
                {
                    // the pivot array is used as the Columns in the report so we resize because this was the final set of columns
                    Array.Resize(ref pivot, dim);
                }
            }

            if (groupby != null)
            {
                Tuple<StatisticsPivot.TableEntry[][], int> result = StatisticsPivot.GroupBy(report.Facts, pivot);

                if (id != null)
                {
                    var col = Array.FindIndex(pivot, s => s.Equals("Version", StringComparison.Ordinal));
                    if (col >= 0)
                    {
                        for (var row = 0; row < result.Item1.GetLength(0); row++)
                        {
                            var entry = result.Item1[row][col];
                            if (entry != null)
                            {
                                entry.Uri = Url.Package(id, entry.Data);
                            }
                        }
                    }
                }

                // We do this here to try to order the result by the Version if available.
                // Since Version might not be available, don't sort if it isn't.
                // If Version is available, we need the following empty version rows to be moved with it (rowspan)
                NuGetVersion prevVersion = null;
                report.Table = result.Item1
                    .Select(e =>
                    {
                        if (NuGetVersion.TryParse(e[0]?.Data, out NuGetVersion versionOut))
                        {
                            prevVersion = versionOut;
                            return new { version = versionOut, e };
                        }

                        return new { version = prevVersion, e };
                    })
                    .OrderByDescending(e => e.version)
                .Select(e => e.e).ToList();
                report.Total = result.Item2;
                report.Columns = pivot.Select(GetDimensionDisplayName);
            }

            if (groupby == null)
            {
                //  degenerate case (but still logically valid)

                foreach (string dimension in dimensions)
                {
                    if (!report.Dimensions.Any(d => d.Value == dimension))
                    {
                        report.Dimensions.Add(new StatisticsDimension
                        {
                            Value = dimension,
                            DisplayName = GetDimensionDisplayName(dimension),
                            IsChecked = false
                        });
                    }
                }

                report.Table = null;
                report.Total = report.Facts.Sum(fact => fact.Amount);
            }
        }

        private static void CheckGroupBy(string[] groupby, string name, string[] pivot, ref int dimension, StatisticsPackagesReport report)
        {
            if (Array.Exists(groupby, s => s.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                pivot[dimension++] = name;
                report.Dimensions.Add(new StatisticsDimension { Value = name, DisplayName = GetDimensionDisplayName(name), IsChecked = true });
            }
            else
            {
                report.Dimensions.Add(new StatisticsDimension { Value = name, DisplayName = GetDimensionDisplayName(name), IsChecked = false });
            }
        }

        private static string GetDimensionDisplayName(string name)
        {
            switch (name)
            {
                case "ClientName":
                    return "Client Name";
                case "ClientVersion":
                    return "Client Version";
                default:
                    return name;
            }
        }

        private static string MakeReportId(string[] groupby)
        {
            string graphId = "report-";
            if (groupby != null)
            {
                foreach (string g in groupby)
                {
                    graphId += g;
                }
            }
            return graphId;
        }

        private bool UseD3()
        {
            //  the aim here is to explicit eliminate IE 7.0 and IE 8.0 from the browsers that support D3
            //  we are doing this on the server rather than in the browser because even downloading the D3 script fails
            bool f = true;
            if (Request != null && Request.Browser != null && Request.Browser.Browser == "IE")
            {
                float version;
                if (float.TryParse(Request.Browser.Version, out version))
                {
                    f = version > 8.0;
                }
            }
            return f;
        }
    }
}
