using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ServicoF1.servicos
{
    internal static class Security
    {
        /// <summary>
        /// Decrypt a string
        /// </summary>
        /// <param name="cipherText"> the encrypted string </param>
        /// <param name="passPhrase"> the chypher used to encrypt </param>
        /// <returns> the string as a plain text. </returns>
        public static string Decrypt(string cipherText)
        {
            try
            {
                string decriptText;
                var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
                // Get the saltbytes by extracting the first 32 bytes from the supplied cipherText bytes.
                var keyValue = cipherTextBytesWithSaltAndIv.Take(32).ToArray();
                // Get the IV bytes by extracting the next 32 bytes from the supplied cipherText bytes.
                var vectorValue = cipherTextBytesWithSaltAndIv.Skip(32).Take(16).ToArray();
                var cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip(48).Take(cipherTextBytesWithSaltAndIv.Length - 48).ToArray();

                cipherText = cipherText.Replace(" ", "+");
                byte[] bytesBuff = cipherTextBytes;

                using (Aes aes = Aes.Create())
                {
                    aes.Key = keyValue;
                    aes.IV = vectorValue;

                    using (var memoryStream = new MemoryStream())
                    {
                        using (var cStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cStream.Write(bytesBuff, 0, bytesBuff.Length);
                            cStream.Close();
                        }

                        decriptText = Encoding.Unicode.GetString(memoryStream.ToArray());
                        memoryStream.Close();
                    }

                    aes.Clear();
                }

                return decriptText;
            }
            catch
            {
                return cipherText;
            }
        }
    }
}