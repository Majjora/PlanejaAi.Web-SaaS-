using System.Security.Cryptography;
using System.Text;

namespace PlanejaAi.Helpers
{
    public static class CriptografiaHelper
    {
        private static string _chaveSecreta = string.Empty;
        private static string _iv = string.Empty;
        public static void Inicializar(string chaveSecreta, string iv)
        {
            _chaveSecreta = chaveSecreta;
            _iv = iv;
        }

        public static string Criptografar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return texto;

            if (string.IsNullOrEmpty(_chaveSecreta) || string.IsNullOrEmpty(_iv))
                throw new InvalidOperationException("As chaves de criptografia não foram configuradas.");

            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(_chaveSecreta);
                aes.IV = Encoding.UTF8.GetBytes(_iv);

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(texto);
                        }
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Descriptografar(string textoCriptografado)
        {
            if (string.IsNullOrEmpty(textoCriptografado)) return textoCriptografado;

            if (string.IsNullOrEmpty(_chaveSecreta) || string.IsNullOrEmpty(_iv))
                throw new InvalidOperationException("As chaves de criptografia não foram configuradas.");

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(_chaveSecreta);
                    aes.IV = Encoding.UTF8.GetBytes(_iv);

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(textoCriptografado)))
                    {
                        using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader sr = new StreamReader(cs))
                            {
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch
            {
                return textoCriptografado;
            }
        }
    }
}