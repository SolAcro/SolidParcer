using System.Collections.Generic;

namespace SolidParcer.Models
{
    public class ApiEvent
    {
        public string id { get; set; }
        public string name { get; set; }
        public string date { get; set; }
        public string location { get; set; }
        public string status { get; set; }
        public bool is_results_visible { get; set; }
    }

    public class ApiDistance
    {
        public string id { get; set; }
        public string name { get; set; }
        public string distance_km { get; set; }
    }

    public class ApiFinishTime
    {
        public int hours { get; set; }
        public int minutes { get; set; }
        public int seconds { get; set; }
        public int milliseconds { get; set; }
    }

    public class ApiResult
    {
        public string id { get; set; }
        public int? place { get; set; }
        public ApiFinishTime finish_time { get; set; }
        public string category { get; set; }
        public int? category_place { get; set; }
        public string result_status { get; set; } // DNF, DNS, DSQ, null
        public int bib_number { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string patronymic { get; set; }
        public string city { get; set; }
        public string team_name { get; set; }
        public string distance_name { get; set; }
        public string distance_km { get; set; }
    }

    public class ApiResultsResponse
    {
        public List<ApiResult> results { get; set; }
    }

    public class ApiDistancesResponse
    {
        public List<ApiDistance> distances { get; set; }
    }

    public class ApiEventsResponse
    {
        public List<ApiEvent> events { get; set; }
    }
}