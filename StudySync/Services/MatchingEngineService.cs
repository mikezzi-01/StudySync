namespace StudySync.Services
{
    public class MatchingEngineService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<MatchingEngineService> _logger;

        public MatchingEngineService(
            HttpClient http,
            IConfiguration config,
            ILogger<MatchingEngineService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Triggers on-demand match computation for a single user.
        /// Called immediately after profile setup or profile update.
        /// Fire-and-forget — does not block the calling request.
        /// </summary>
        public async Task TriggerMatchesForUserAsync(int userId)
        {
            try
            {
                var baseUrl = _config["MatchingEngine:BaseUrl"]
                              ?? "http://localhost:5050";

                var response = await _http.PostAsync(
                    $"{baseUrl}/compute/{userId}",
                    null
                );

                if (response.IsSuccessStatusCode)
                    _logger.LogInformation(
                        "[MatchingEngine] Triggered match computation for UserID {UserId}.", userId);
                else
                    _logger.LogWarning(
                        "[MatchingEngine] Failed for UserID {UserId}. Status: {Status}",
                        userId, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "[MatchingEngine] Could not reach engine: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Checks if the matching engine API is reachable.
        /// </summary>
        public async Task<bool> IsEngineRunningAsync()
        {
            try
            {
                var baseUrl = _config["MatchingEngine:BaseUrl"] ?? "http://localhost:5050";
                var response = await _http.GetAsync($"{baseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
