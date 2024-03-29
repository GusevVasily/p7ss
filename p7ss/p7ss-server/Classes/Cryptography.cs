﻿using System;
using System.IO;
using System.Security.Cryptography;

namespace p7ss_server.Classes
{
    internal class Cryptography : Core
    {
        private static readonly byte[] ServerSalt = { 0xc9, 0xd0, 0xfa, 0x017, 0xd9, 0x4e, 0x6b, 0xaa, 0x1e, 0x0a, 0x1b, 0xcc, 0x6f, 0x2b, 0x43, 0x1a };
        private static readonly byte[] ClientSalt = { 0x34, 0x3d, 0x77, 0x00, 0x01, 0x0d, 0xc4, 0xa8, 0x39, 0xed, 0x10, 0xbb, 0xa8, 0xf4, 0x0b, 0x19 };

        internal static string EnCrypt(string plainText, string hash, bool inVault)
        {
            try
            {
                using (Rijndael rijndael = Rijndael.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(hash, inVault ? ServerSalt : ClientSalt);
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

        internal static string DeCrypt(string plainText, string hash, bool inVault)
        {
            try
            {
                using (Rijndael rijndael = Rijndael.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(hash, inVault ? ServerSalt : ClientSalt);
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
