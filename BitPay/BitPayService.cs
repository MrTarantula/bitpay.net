using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using BitPay.Models.Settlements;
using BitCoinSharp;
using System.Linq;
using BitPay.Models.Tokens;

namespace BitPay
{
    public class BitPayService
    {
        private const string BITPAY_API_VERSION = "2.0.0";
        private const string BITPAY_PLUGIN_INFO = "BitPay CSharp Client " + BITPAY_API_VERSION;
        private const string BITPAY_URL = "https://bitpay.com/";

        public const string FACADE_PAYROLL = "payroll";
        public const string FACADE_POS = "pos";
        public const string FACADE_MERCHANT = "merchant";
        public const string FACADE_USER = "user";

        private HttpClient _httpClient;
        private string _baseUrl = BITPAY_URL;
        private EcKey _ecKey;
        private string _identity = "";
        private string _clientName = "";
        private Dictionary<string, string> _tokenCache; // {facade, token}

        /// <summary>
        /// Constructor for use if the keys and SIN are managed by this library.
        /// </summary>
        /// <param name="keyString">Private key string.</param>
        /// <param name="clientName">The label for this client.</param>
        /// <param name="envUrl">The target server URL.</param>
        public BitPayService(string keyString = "", string clientName = BITPAY_PLUGIN_INFO, string envUrl = BITPAY_URL)
        {
            if (clientName.Equals(BITPAY_PLUGIN_INFO))
            {
                clientName += " on " + Environment.MachineName;
            }
            // Eliminate special characters from the client name (used as a token label).  Trim to 60 chars.
            string _clientName = new Regex("[^a-zA-Z0-9_ ]").Replace(clientName, "_");
            if (_clientName.Length > 60)
            {
                _clientName = _clientName.Substring(0, 60);
            }

            _baseUrl = envUrl;
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(_baseUrl)
            };

            if (!String.IsNullOrWhiteSpace(keyString))
            {
                _ecKey = KeyUtils.CreateEcKeyFromHexString(keyString);
            }
            else
            {
                if (KeyUtils.PrivateKeyExists())
                {
                    _ecKey = KeyUtils.LoadEcKey();
                }
                else
                {
                    _ecKey = KeyUtils.CreateEcKey();
                    KeyUtils.SaveEcKey(_ecKey);
                }
            }
            _identity = KeyUtils.DeriveSIN(_ecKey);

            _tokenCache = new Dictionary<string, string>();
            var response = Get<List<Dictionary<string, string>>>("tokens", new Dictionary<string, string>());

            _tokenCache = response.SelectMany(t => t).ToDictionary(r => r.Key, r => r.Value);
        }

        /// <summary>
        /// Constructor for use if the keys and SIN were derived external to this library.
        /// </summary>
        /// <param name="ecKey">An elliptical curve key.</param>
        /// <param name="clientName">The label for this client.</param>
        /// <param name="envUrl">The target server URL.</param>
        public BitPayService(EcKey ecKey, string clientName = BITPAY_PLUGIN_INFO, string envUrl = BITPAY_URL)
        {
            _ecKey = ecKey;
            _identity = KeyUtils.DeriveSIN(_ecKey);
            _baseUrl = envUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl)
            };

            _tokenCache = new Dictionary<string, string>();
            var response = Get<List<Dictionary<string, string>>>("tokens", new Dictionary<string, string>());

            _tokenCache = response.SelectMany(t => t).ToDictionary(r => r.Key, r => r.Value);
        }

        /// <summary>
        /// Return the identity of this client.
        /// </summary>
        public String Identity
        {
            get { return _identity; }
        }

        /// <summary>
        /// Authorize (pair) this client with the server using the specified pairing code.
        /// </summary>
        /// <param name="pairingCode">A code obtained from the server; typically from bitpay.com/api-tokens.</param>
        public void AuthorizeClient(String pairingCode)
        {
            Token token = new Token
            {
                Id = _identity,
                Guid = Guid.NewGuid().ToString(),
                PairingCode = pairingCode,
                Label = _clientName
            };

            var tokens = Post<List<Token>>("tokens", JsonConvert.SerializeObject(token));

            foreach (Token t in tokens)
            {
                _tokenCache.Add(t.Facade, t.Value);
            }
        }

        /// <summary>
        /// Request authorization (a token) for this client in the specified facade.
        /// </summary>
        /// <param name="facade">The facade for which authorization is requested.</param>
        /// <returns>A pairing code for this client.  This code must be used to authorize this client at BitPay.com/api-tokens.</returns>
        public String RequestClientAuthorization(String facade)
        {
            Token token = new Token
            {
                Id = _identity,
                Guid = Guid.NewGuid().ToString(),
                Facade = facade,
                Count = 1,
                Label = _clientName
            };

            string json = JsonConvert.SerializeObject(token);
            var tokens = Post<List<Token>>("tokens", json);

            if (tokens.Count != 1)
            {
                throw new BitPayException("Error - failed to get token resource; expected 1 token, got " + tokens.Count);
            }
            _tokenCache.Add(tokens[0].Facade, tokens[0].Value);
            return tokens[0].PairingCode;
        }

        /// <summary>
        /// Specified whether the client has authorization (a token) for the specified facade.
        /// </summary>
        /// <param name="facade">The facade name for which authorization is tested.</param>
        /// <returns></returns>
        public bool ClientIsAuthorized(String facade)
        {
            return _tokenCache.ContainsKey(facade);
        }

        /// <summary>
        /// Retrieves settlement reports for the calling merchant filtered by query. The `limit` and `offset` parameters specify pages for large query sets.
        /// </summary>
        /// <param name="currency">The three digit currency string for the ledger to retrieve.</param>
        /// <param name="dateStart">The start date for the query.</param>
        /// <param name="dateEnd">The end date for the query.</param>
        /// <param name="status">Can be `processing`, `completed`, or `failed`.</param>
        /// <param name="limit">Maximum number of settlements to retrieve.</param>
        /// <param name="offset">Offset for paging</param>
        /// <returns></returns>
        public List<Settlement> GetSettlements(string currency, DateTime dateStart, DateTime dateEnd, string status = "", int limit = 100, int offset = 0)
        {
            var parameters = new Dictionary<string, string>
            {
                { "token", GetAccessToken(FACADE_MERCHANT) },
                { "startDate", $"{dateStart.ToShortDateString()}" },
                { "endDate", $"{dateEnd.ToShortDateString()}" },
                { "currency", currency },
                { "status", status },
                { "limit", $"{limit}" },
                { "offset", $"{offset}" }
            };

            return Get<List<Settlement>>("settlements", parameters);
        }

        /// <summary>
        /// Retrieves a summary of the specified settlement.
        /// </summary>
        /// <param name="settlementId"></param>
        /// <returns></returns>
        public Settlement GetSettlement(string settlementId)
        {
            var parameters = new Dictionary<string, string>
            {
                { "token", GetAccessToken(FACADE_MERCHANT) }
            };

            return Get<Settlement>($"settlements/{settlementId}", parameters);
        }

        /// <summary>
        /// Gets a detailed reconciliation report of the activity within the settlement period
        /// </summary>
        /// <param name="settlementId"></param>
        /// <returns></returns>
        public Settlement GetSettlementReconciliationReport(Settlement settlement)
        {
            var parameters = new Dictionary<string, string>
            {
                { "token", settlement.Token }
            };

            return Get<Settlement>($"settlements/{settlement.Id}/reconciliationReport", parameters);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private T Get<T>(String uri, Dictionary<string, string> parameters = null)
        {
            try
            {
                String fullURL = _baseUrl + uri;
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-accept-version", BITPAY_API_VERSION);
                _httpClient.DefaultRequestHeaders.Add("x-bitpay-plugin-info", BITPAY_PLUGIN_INFO);
                if (parameters != null)
                {
                    fullURL += "?";
                    foreach (KeyValuePair<string, string> entry in parameters)
                    {
                        fullURL += entry.Key + "=" + entry.Value + "&";
                    }
                    fullURL = fullURL.Substring(0, fullURL.Length - 1);

                    _httpClient.DefaultRequestHeaders.Add("x-signature", KeyUtils.Sign(_ecKey, fullURL));
                    _httpClient.DefaultRequestHeaders.Add("x-identity", KeyUtils.BytesToHex(_ecKey.PubKey));
                }

                var response = _httpClient.GetAsync(fullURL).Result;
                response.EnsureSuccessStatusCode();
                var body = response.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<Result<T>>(body.Result).Data;
            }
            catch (Exception ex)
            {
                throw new BitPayException("Error: " + ex.ToString());
            }
        }

        private HttpResponseMessage Delete(String uri, Dictionary<string, string> parameters = null)
        {
            try
            {
                String fullURL = _baseUrl + uri;
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-accept-version", BITPAY_API_VERSION);
                _httpClient.DefaultRequestHeaders.Add("x-bitpay-plugin-info", BITPAY_PLUGIN_INFO);

                if (parameters != null)
                {
                    fullURL += "?";
                    foreach (KeyValuePair<string, string> entry in parameters)
                    {
                        fullURL += entry.Key + "=" + entry.Value + "&";
                    }
                    fullURL = fullURL.Substring(0, fullURL.Length - 1);
                    String signature = KeyUtils.Sign(_ecKey, fullURL);
                    _httpClient.DefaultRequestHeaders.Add("x-signature", signature);
                    _httpClient.DefaultRequestHeaders.Add("x-identity", KeyUtils.BytesToHex(_ecKey.PubKey));
                }

                var result = _httpClient.DeleteAsync(fullURL).Result;
                return result;
            }
            catch (Exception ex)
            {
                throw new BitPayException("Error: " + ex.ToString());
            }
        }

        private T Post<T>(String uri, String json, bool signatureRequired = false)
        {
            try
            {
                byte[] unicodeBytes = Encoding.Unicode.GetBytes(json);
                byte[] asciiBytes = Encoding.Convert(Encoding.Unicode, Encoding.ASCII, unicodeBytes);
                char[] asciiChars = new char[Encoding.ASCII.GetCharCount(asciiBytes, 0, asciiBytes.Length)];
                Encoding.ASCII.GetChars(asciiBytes, 0, asciiBytes.Length, asciiChars, 0);

                var bodyContent = new StringContent(new string(asciiChars));

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-accept-version", BITPAY_API_VERSION);
                _httpClient.DefaultRequestHeaders.Add("x-bitpay-plugin-info", BITPAY_PLUGIN_INFO);
                bodyContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                if (signatureRequired)
                {
                    String signature = KeyUtils.Sign(_ecKey, _baseUrl + uri + json);
                    _httpClient.DefaultRequestHeaders.Add("x-signature", signature);
                    _httpClient.DefaultRequestHeaders.Add("x-identity", KeyUtils.BytesToHex(_ecKey.PubKey));
                }
                var response = _httpClient.PostAsync(uri, bodyContent).Result;
                response.EnsureSuccessStatusCode();
                var body = response.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<Result<T>>(body.Result).Data;
            }
            catch (Exception ex)
            {
                throw new BitPayException("Error: " + ex.ToString());
            }
        }

        private String GetAccessToken(String key)
        {
            if (!_tokenCache.ContainsKey(key))
            {
                throw new BitPayException("Error: You do not have access to facade: " + key);
            }
            return _tokenCache[key];
        }
    }

    /// <summary>
    /// Generic Response object, used to deserialize data object from BitPay
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class Result<T>
    {
        public string Facade { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }
        public List<string> Errors { get; set; }
    }
}
