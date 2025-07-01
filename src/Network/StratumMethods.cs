namespace BitcoinMinerConsole.Network;

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