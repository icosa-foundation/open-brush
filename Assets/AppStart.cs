using UnityEngine;
using System;
using System.Security.Cryptography;
using System.Text;

public class AppStart : MonoBehaviour
{
    private const string firstRunKey = "FirstRunTimeEncrypted_1212"; // 加密存储的首次运行时间
    private const int expirationTime = 5 * 24 * 60 * 60; // 设置为7天过期（单位：秒）
    private const string LastValidTime = "_lastValidTime_1212";
    private DateTime _lastValidTime;//每次启动程序校验时间

    void Start()
    {
        if (!PlayerPrefs.HasKey(firstRunKey)) // 判断是否是第一次启动
        {
            long firstRunTime = DateTime.UtcNow.Ticks;
            string encryptedTime = Encrypt(firstRunTime.ToString());
            PlayerPrefs.SetString(firstRunKey, encryptedTime);
            PlayerPrefs.SetString(LastValidTime, DateTime.UtcNow.ToString());
            PlayerPrefs.Save();
        }
        else
        {
            string encryptedTime = PlayerPrefs.GetString(firstRunKey);
            string decryptedTime = Decrypt(encryptedTime);
            long firstRunTime = long.Parse(decryptedTime);
            long currentTime = DateTime.UtcNow.Ticks;
            long elapsedTime = (currentTime - firstRunTime) / TimeSpan.TicksPerSecond;

            ValidateTime();

            Debug.LogError("elapsedTime:" + elapsedTime + "   " + expirationTime + "--Validatestate:" + Validatestate + "");

            if (elapsedTime < 0)
            {
                Debug.Log("时间异常退出");
                Application.Quit(); // 超过30天后退出应用
            }

            if (elapsedTime > expirationTime || !Validatestate) // 判断是否超过30天或者修改系统时间
            {
                Debug.Log("超出时间程序退出");
                Application.Quit(); // 超过30天后退出应用
            }
        }
    }


    private void LateUpdate()
    {
        RecoreUseTime();
    }


    float timeSpan = 300f;//时间间隔秒
    private float startTime = 0; //临时时间
    private bool Validatestate = true;

    void RecoreUseTime()
    {
        ////同步用户身体数据
        float elapsedTime1 = Time.time - startTime;
        if (elapsedTime1 > timeSpan && Validatestate)//同步间隔间隔
        {
            PlayerPrefs.SetString(LastValidTime, DateTime.UtcNow.ToString());
            startTime = Time.time;
        }
    }

    bool ValidateTime()
    {
        string _tmpdatelast = PlayerPrefs.GetString(LastValidTime, "");
        DateTime.TryParse(_tmpdatelast, out _lastValidTime);
        if (_lastValidTime > DateTime.UtcNow)
        {
            Validatestate = false;
            // 时间出现异常
            return false;
        }
        return true;
    }


    string Encrypt(string plainText)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = new byte[16]; // 设置一个固定的密钥
            aesAlg.IV = new byte[16]; // 设置一个固定的IV
            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }
    }

    string Decrypt(string cipherText)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = new byte[16]; // 与加密时相同的密钥
            aesAlg.IV = new byte[16]; // 与加密时相同的IV
            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
