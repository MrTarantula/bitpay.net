using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace BitPay.Models.Tokens
{
    public class Token
    {
        public Token() { }
        public string Guid { get; set; }
        public long Nonce { get; set; }
        public string Id { get; set; }
        public string PairingCode { get; set; }
        public string Facade { get; set; }
        public string Label { get; set; }
        public int Count { get; set; }
        public long PairingExpiration { get; set; }
        public List<Policy> Policies { get; set; }
        public string Resource { get; set; }
        public string Value { get; set; }
        public long DateCreated { get; set; }
    }

    public class Policy
    {
        [JsonProperty(PropertyName = "policy")]
        public string Value { get; set; }
        public string Method { get; set; }
        public List<String> Params { get; set; }
    }
}
