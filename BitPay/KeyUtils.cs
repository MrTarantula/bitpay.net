using BitCoinSharp;
using Org.BouncyCastle.Math;
using System;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace BitPay
{
    public static class KeyUtils
    {
        private static char[] hexArray = "0123456789abcdef".ToCharArray();
        private static string PRIV_KEY_FILENAME = "bitpay_private.key";

        public static bool PrivateKeyExists()
        {
            return File.Exists(PRIV_KEY_FILENAME);
        }

        public static EcKey CreateEcKey()
        {
            //Default constructor uses SecureRandom numbers.
            return new EcKey();
        }

        public static EcKey CreateEcKeyFromHexString(String privateKey)
        {
            BigInteger pkey = new BigInteger(privateKey, 16);
            EcKey key = new EcKey(pkey);
            return key;
        }

        // Convenience method.
        public static EcKey CreateEcKeyFromHexStringFile(String privKeyFile)
        {
            String privateKey = GetKeyStringFromFile(privKeyFile);
            return CreateEcKeyFromHexString(privateKey);
        }

        public static EcKey LoadEcKey()
        {
            using (FileStream fs = File.OpenRead(PRIV_KEY_FILENAME))
            {
                byte[] b = new byte[1024];
                fs.Read(b, 0, b.Length);
                EcKey key = EcKey.FromAsn1(b);
                return key;
            }
        }

        public static String GetKeyStringFromFile(String filename)
        {
            StreamReader sr;
            try
            {
                sr = new StreamReader(filename);
                String line = sr.ReadToEnd();
                sr.Close();
                return line;
            }
            catch (IOException e)
            {
                Console.Write(e.Message);
            }
            return "";
        }

        public static void SaveEcKey(EcKey ecKey)
        {
            byte[] bytes = ecKey.ToAsn1();
            FileStream fs = new FileStream(PRIV_KEY_FILENAME, FileMode.Create, FileAccess.Write);
            fs.Write(bytes, 0, bytes.Length);
            fs.Close();
        }

        public static String DeriveSIN(EcKey ecKey)
        {
            String preSIN = "0F02" + BytesToHex(ecKey.PubKeyHash);

            // Convert the hex string back to binary and double sha256 hash it leaving in binary both times
            byte[] preSINbyte = HexToBytes(preSIN);
            byte[] hash2Bytes = Utils.DoubleDigest(preSINbyte);

            // Convert back to hex and take first four bytes
            String hashString = BytesToHex(hash2Bytes);
            String first4Bytes = hashString.Substring(0, 8);

            // Append first four bytes to fully appended SIN string
            String unencoded = preSIN + first4Bytes;
            byte[] unencodedBytes = new BigInteger(unencoded, 16).ToByteArray();
            String encoded = Base58.Encode(unencodedBytes);

            return encoded;
        }

        public static String Sign(EcKey ecKey, String input)
        {
            String hash = Sha256Hash(input);
            return BytesToHex(ecKey.Sign(HexToBytes(hash)));
        }

        private static String Sha256Hash(String value)
        {
            StringBuilder Sb = new StringBuilder();
            using (var hash = SHA256.Create())
            {
                Encoding enc = Encoding.UTF8;
                Byte[] result = hash.ComputeHash(enc.GetBytes(value));

                foreach (Byte b in result)
                    Sb.Append(b.ToString("x2"));
            }
            return Sb.ToString();
        }

        private static int GetHexVal(char hex) => hex - (hex < 58 ? 48 : (hex < 97 ? 55 : 87));

        private static bool IsValidHexDigit(char chr) => ('0' <= chr && chr <= '9') || ('a' <= chr && chr <= 'f') || ('A' <= chr && chr <= 'F');

        public static byte[] HexToBytes(string hex)
        {
            if (hex == null)
                throw new ArgumentNullException("hex");
            if (hex.Length % 2 == 1)
                throw new FormatException("The binary key cannot have an odd number of digits");

            if (hex == string.Empty)
                return new byte[0];

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                char highNibble = hex[i << 1];
                char lowNibble = hex[(i << 1) + 1];

                if (!IsValidHexDigit(highNibble) || !IsValidHexDigit(lowNibble))
                    throw new FormatException("The binary key contains invalid chars.");

                arr[i] = (byte)((GetHexVal(highNibble) << 4) + (GetHexVal(lowNibble)));
            }
            return arr;
        }

        public static String BytesToHex(byte[] bytes)
        {
            char[] hexChars = new char[bytes.Length * 2];
            for (int j = 0; j < bytes.Length; j++)
            {
                int v = bytes[j] & 0xFF;
                hexChars[j * 2] = hexArray[(int)((uint)v >> 4)];
                hexChars[j * 2 + 1] = hexArray[v & 0x0F];
            }
            return new String(hexChars);
        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}