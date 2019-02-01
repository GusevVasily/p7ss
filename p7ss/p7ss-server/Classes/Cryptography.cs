using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace p7ss_server.Classes
{
    internal class Cryptography : Core
    {
        internal static byte[] EnCrypt(string plainText)
        {
            try
            {
                using (Rijndael rijndael = Rijndael.Create())
                {
                    rijndael.Mode = CipherMode.CBC;
                    rijndael.IV = Encoding.UTF8.GetBytes(CryptographyIv);
                    rijndael.Key = Encoding.UTF8.GetBytes(CryptographyKey);
                    rijndael.Padding = PaddingMode.PKCS7;

                    ICryptoTransform encryptor = rijndael.CreateEncryptor();

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                            {
                                streamWriter.Write(plainText);
                            }
                        }

                        return memoryStream.ToArray();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static string DeCrypt(byte[] plainText)
        {
            try
            {
                using (Rijndael rijndael = Rijndael.Create())
                {
                    rijndael.Mode = CipherMode.CBC;
                    rijndael.IV = Encoding.UTF8.GetBytes(CryptographyIv);
                    rijndael.Key = Encoding.UTF8.GetBytes(CryptographyKey);
                    rijndael.Padding = PaddingMode.PKCS7;

                    ICryptoTransform decryptor = rijndael.CreateDecryptor();

                    using (MemoryStream memoryStream = new MemoryStream(plainText))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
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
