using System;
using System.IO;
using System.Security.Cryptography;

namespace p7ss_client.Classes
{
    internal class Cryptography : Core
    {
        private static readonly byte[] Salt = { 0x34, 0x3d, 0x77, 0x00, 0x01, 0x0d, 0xc4, 0xa8, 0x39, 0xed, 0x10, 0xbb, 0xa8, 0xf4, 0x0b, 0x19 };

        internal static string EnCrypt(string plainText, string hash)
        {
            try
            {
                using (Rijndael rijndael = Rijndael.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(hash, Salt);
                    rijndael.Mode = CipherMode.CBC;
                    rijndael.IV = pdb.GetBytes(16);
                    rijndael.Key = pdb.GetBytes(32);
                    rijndael.Padding = PaddingMode.PKCS7;
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, rijndael.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                            {
                                streamWriter.Write(plainText);
                            }
                        }

                        return Convert.ToBase64String(memoryStream.ToArray());
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static string DeCrypt(string plainText, string hash)
        {
            try
            {
                using (Rijndael rijndael = Rijndael.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(hash, Salt);
                    rijndael.Mode = CipherMode.CBC;
                    rijndael.IV = pdb.GetBytes(16);
                    rijndael.Key = pdb.GetBytes(32);
                    rijndael.Padding = PaddingMode.PKCS7;
                    using (MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(plainText)))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, rijndael.CreateDecryptor(), CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader(cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
