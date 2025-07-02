using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LotteryBitcoinMiner.Network
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
   

}
