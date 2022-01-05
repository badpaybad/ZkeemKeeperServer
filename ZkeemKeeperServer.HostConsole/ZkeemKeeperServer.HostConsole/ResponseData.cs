namespace ZkeemKeeperServer.HostConsole
{
    public class ResponseData
    {
        public string Body { get; set; }
        public byte[] DataInByte { get; set; }

        public string StatusCode { get; set; } = "200 OK";
        public string ContentType { get; set; } = "text/plain";
    }

}
