using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitcoinMinerConsole.Network
{
    public class StratumMessage
    {
        [JsonProperty("id")]
        public object? Id { get; set; }

        [JsonProperty("method")]
        public string? Method { get; set; }

        [JsonProperty("params")]
        public JArray? Params { get; set; }

        [JsonProperty("result")]
        public object? Result { get; set; }

        [JsonProperty("error")]
        public object? Error { get; set; }

        public bool IsRequest => !string.IsNullOrEmpty(Method);
        public bool IsResponse => Id != null && Method == null;
        public bool IsNotification => Id == null && !string.IsNullOrEmpty(Method);

        public static StratumMessage CreateRequest(int id, string method, params object[] parameters)
        {
            return new StratumMessage
            {
                Id = id,
                Method = method,
                Params = new JArray(parameters)
            };
        }

        public static StratumMessage CreateResponse(object id, object result)
        {
            return new StratumMessage
            {
                Id = id,
                Result = result
            };
        }

        public static StratumMessage CreateErrorResponse(object id, object error)
        {
            return new StratumMessage
            {
                Id = id,
                Error = error
            };
        }

        public static StratumMessage CreateNotification(string method, params object[] parameters)
        {
            return new StratumMessage
            {
                Method = method,
                Params = new JArray(parameters)
            };
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        public static StratumMessage? FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<StratumMessage>(json);
            }
            catch
            {
                return null;
            }
        }

        public T? GetParam<T>(int index)
        {
            if (Params == null || index >= Params.Count)
                return default;

            try
            {
                return Params[index].ToObject<T>();
            }
            catch
            {
                return default;
            }
        }

        public string GetParamAsString(int index)
        {
            return GetParam<string>(index) ?? "";
        }

        public int GetParamAsInt(int index)
        {
            return GetParam<int>(index);
        }

        public bool GetParamAsBool(int index)
        {
            return GetParam<bool>(index);
        }

        public string[] GetParamAsStringArray(int index)
        {
            try
            {
                var param = Params?[index];
                if (param is JArray array)
                {
                    return array.Select(x => x.ToString()).ToArray();
                }
                return Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public override string ToString()
        {
            if (IsRequest)
                return $"Request: {Method} (ID: {Id})";
            if (IsResponse)
                return $"Response (ID: {Id})";
            if (IsNotification)
                return $"Notification: {Method}";
            return "Unknown message type";
        }
    }

    public static class StratumMethods
    {
        // Client to server methods
        public const string Subscribe = "mining.subscribe";
        public const string Authorize = "mining.authorize";
        public const string Submit = "mining.submit";
        public const string ExtraNonceSubscribe = "mining.extranonce.subscribe";

        // Server to client methods
        public const string Notify = "mining.notify";
        public const string SetDifficulty = "mining.set_difficulty";
        public const string SetExtraNonce = "mining.set_extranonce";
        public const string Reconnect = "client.reconnect";
        public const string GetVersion = "client.get_version";
        public const string ShowMessage = "client.show_message";
    }

    public class StratumError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = "";

        public static class Codes
        {
            public const int UnknownMethod = 20;
            public const int JobNotFound = 21;
            public const int DuplicateShare = 22;
            public const int LowDifficultyShare = 23;
            public const int UnauthorizedWorker = 24;
            public const int NotSubscribed = 25;
        }
    }
}
