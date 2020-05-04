using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.ApplicationInsights;

namespace Griffin.StressTest.Web.Controllers
{
    [Route("api")]
    [ApiController]
    public class StressController : ControllerBase
    {
        private readonly TelemetryClient _telemetryClient;

        public StressController(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient;
        }

        static StressController()
        {
        }

        [Route("db")]
        [HttpGet]
        [HttpOptions]
        public async Task<IActionResult> GetDbResult()
        {
            var sql = @"SELECT p.ProductID,
                        p.Name,
                        pm.Name,
                        p.StandardCost,
                        p.ListPrice,
                        p.Size
                        FROM SalesLT.Product p
                        INNER JOIN SalesLT.ProductModel pm
                        ON pm.ProductModelID = p.ProductModelID; ";

            using (var sqlConnection = new SqlConnection(Environment.GetEnvironmentVariable("SQL")))
            {
                var results = await sqlConnection.QueryAsync(sql);

                _telemetryClient.TrackEvent("DB Request", new Dictionary<string, string>()
                {
                    {"query", sql },
                    {"number", "42" }
                });

                return Ok(results);
            }
        }

        private static DateTime _lastMetricSync { get; set; } = DateTime.UtcNow;
        private static List<int> _delays { get; set; } = new List<int>();

        [Route("random")]
        [HttpGet]
        [HttpOptions]
        public async Task<IActionResult> Delay()
        {
            var random = new Random();
            var num = random.Next(1, 10000);

            _delays.Add(num);

            /*** COMMENT OUT FOR DEMOS
            
            // check if we should sync metrics
            var now = DateTime.UtcNow;
            if ((now - _lastMetricSync).TotalMinutes > 1)
            {
                var avg = _delays.Average();
                _telemetryClient.TrackMetric("AvgDelay", avg);

                _delays.Clear();
                _lastMetricSync = now;
            }
            */

            await Task.Delay(num);

            if (num >= 9990) throw new Exception("Random number was greater than 9900");

            return Ok(num);
        }

        public static readonly object lockObj = new object();
        private static DateTime _lastLocalMetricSync { get; set; } = DateTime.UtcNow;
        private static Dictionary<string, Dictionary<string, int>> _states { get; set; } = new Dictionary<string, Dictionary<string, int>>();

        [Route("locale/{state}/{city}")]
        [HttpGet]
        [HttpOptions]
        public IActionResult FromLocale(string state, string city)
        {
            if (string.IsNullOrWhiteSpace(state)) return BadRequest();
            if (string.IsNullOrWhiteSpace(city)) return BadRequest();

            /*** COMMENT OUT FOR DEMOS
            lock (lockObj)// thread safety for the demo :)
            {
                if (!_states.ContainsKey(state)) _states[state] = new Dictionary<string, int>();
                if (!_states[state].ContainsKey(city)) _states[state][city] = 0;

                _states[state][city] = _states[state][city] + 1;

                var now = DateTime.UtcNow;
                if ((now - _lastLocalMetricSync).TotalSeconds > 30)
                {
                    // compute locales
                    Metric metric = _telemetryClient.GetMetric("Api Hits", "State", "City");

                    foreach (var stateKey in _states.Keys)
                    {
                        foreach (var cityKey in _states[stateKey].Keys)
                        {
                            metric.TrackValue(_states[stateKey][cityKey], stateKey, cityKey);
                        }
                    }

                    _states.Clear();
                    _lastLocalMetricSync = now;
                }
            }
            ***/

            return Ok();
        }
    }
}