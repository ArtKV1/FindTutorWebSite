using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Session;
using System;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using System.Collections;

namespace FindTutorWebSite.Controllers
{
    public class AuthController : Controller
    {

        private readonly string connectionString = "Host=HOST;Username=USERNAME;Password=PASSWORD;Database=DATABASE";
        public IActionResult Login([FromBody] Models.TelegramUserData userData)
        {
            HttpContext.Session.SetString("auth_date", $"{userData.auth_date}");
            HttpContext.Session.SetString("first_name", $"{userData.first_name}");
            HttpContext.Session.SetString("id", $"{userData.id}");
            HttpContext.Session.SetString("last_name", $"{userData.last_name}");
            HttpContext.Session.SetString("auth_date", $"{userData.auth_date}");
            HttpContext.Session.SetString("photo_url", $"{userData.photo_url}");
            HttpContext.Session.SetString("username", $"{userData.username}");
            string hash = CryptSession(userData.hash);
            HttpContext.Session.SetString("hash", $"{hash}");

            return Json(new { auth = true });
        }

        public IActionResult CheckAuth()
        {
            string auth_date = HttpContext.Session.GetString("auth_date");
            string first_name = HttpContext.Session.GetString("first_name");
            string id = HttpContext.Session.GetString("id");
            string last_name = HttpContext.Session.GetString("last_name");
            string photo_url = HttpContext.Session.GetString("photo_url");
            string username = HttpContext.Session.GetString("username");
            string hash = HttpContext.Session.GetString("hash");
            if (hash != null)
            {
                hash = DecryptSession(hash);
                string reHash = CryptSession(hash);
                HttpContext.Session.SetString("hash", $"{reHash}");
            }
            string dataCheckString = $"auth_date={auth_date}";

            if (first_name != null && first_name != "")
                dataCheckString += $"\nfirst_name={first_name}";
            dataCheckString += $"\nid={id}";
            if (last_name != null && last_name != "")
                dataCheckString += $"\nlast_name={last_name}";
            if (photo_url != null && photo_url != "")
                dataCheckString += $"\nphoto_url={photo_url}";
            if (username != null && username != "")
                dataCheckString += $"\nusername={username}";

            string botToken = "BOT_TOKEN";

            byte[] secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));

            using (var hmac = new HMACSHA256(secretKey))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
                string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                if (computedHash == hash)
                {
                    return Json(new { auth = true });
                }
                else
                {
                    HttpContext.Session.Clear();
                    using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                    {

                        connection.Open();

                        string sql = $"DELETE FROM userHash WHERE hash = '{hash}';";

                        using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }

            return Json(new { auth = false } );
        }

        private string CryptSession(string hash)
        {
            Random random = new Random();

            if (hash == null || hash == "")
            {
                return null;
            }

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            StringBuilder stringBuilder = new StringBuilder(256);
            StringBuilder stringBuilder2 = new StringBuilder(256);
            for (int i = 0; i < 32; i++)
            {
                stringBuilder.Append(chars[random.Next(chars.Length)]);
            }

            for (int i = 0; i < 16; i++)
            {
                stringBuilder2.Append(chars[random.Next(chars.Length)]);
            }

            string key = stringBuilder.ToString();
            string iv = stringBuilder2.ToString();
            byte[] Key = Encoding.UTF8.GetBytes(key);
            byte[] IV = Encoding.UTF8.GetBytes(iv);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(hash);
                        }

                        byte[] currentHash = msEncrypt.ToArray();

                        string hashString = BitConverter.ToString(currentHash).Replace("-", "");

                        using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                        {

                            connection.Open();
                           
                            using (NpgsqlCommand command = new NpgsqlCommand($"INSERT INTO userHash (hash, key, iv) values ('{hashString}', '{key}', '{iv}')", connection))
                            {
                                command.Parameters.AddWithValue("@hashValue", NpgsqlTypes.NpgsqlDbType.Bytea, currentHash);

                                command.ExecuteNonQuery();
                            }
                        }

                        return hashString;
                    }
                }
            }
        }

        private string DecryptSession(string hash)
        {
            if (hash == null || hash == "")
            {
                return null;
            }

            string key = "";
            string iv = "";
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                using (NpgsqlCommand command = new NpgsqlCommand($"SELECT key, iv from userHash where hash = '{hash}'", connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            key = reader.GetString(0);
                            iv = reader.GetString(1);
                        }
                    }
                }
            }
            byte[] cipherText = new byte[hash.Length / 2];
            for (int i = 0; i < cipherText.Length; i++)
            {
                cipherText[i] = Convert.ToByte(hash.Substring(i * 2, 2), 16);
            }
            byte[] Key = Encoding.UTF8.GetBytes(key);
            byte[] IV = Encoding.UTF8.GetBytes(iv);
            if (Key.Length != 32 ||  IV.Length != 16) 
            {
                return null;
            }
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                            {

                                connection.Open();

                                string sql = $"DELETE FROM userHash WHERE hash = '{hash}';";

                                using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                                {
                                    command.ExecuteNonQuery();
                                }
                            }
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
