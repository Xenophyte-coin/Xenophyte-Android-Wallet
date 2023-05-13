
using System;
#if DEBUG
using System.Diagnostics;
#endif
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Xenophyte_Connector_All.Setting;
using Xenophyte_Connector_All.Utils;

namespace XenophyteAndroidWallet
{
    public class ClassUtility
    {

        private static readonly List<string> ListOfCharacters = new List<string>
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u",
            "v", "w", "x", "y", "z"
        };

        private static readonly List<string> ListOfNumbers = new List<string> { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private static readonly List<string> ListOfSpecialCharacters = new List<string> { "&", "~", "#", "@", "'", "(", "\\", ")", "=" };

        /// <summary>
        /// Make a new genesis key for dynamic encryption.
        /// </summary>
        /// <returns></returns>
        public static string MakeRandomWalletPassword()
        {
            string walletPassword = string.Empty;
            while (walletPassword.Length < ClassConnectorSetting.WalletMinPasswordLength)
            {
                walletPassword = string.Empty;
                while (!CheckSpecialCharacters(walletPassword) || !CheckLetter(walletPassword) || !CheckNumber(walletPassword))
                {
                    for (int i = 0; i < ClassUtils.GetRandomBetween(ClassConnectorSetting.WalletMinPasswordLength, ClassConnectorSetting.WalletMinPasswordLength * 2); i++)
                    {
                        var randomUpper = ClassUtils.GetRandomBetween(0, 100);
                        if (randomUpper <= 30)
                            walletPassword += ListOfCharacters[ClassUtils.GetRandomBetween(0, ListOfCharacters.Count - 1)];
                        
                        else if (randomUpper > 30 && randomUpper <= 50)
                            walletPassword += ListOfCharacters[ClassUtils.GetRandomBetween(0, ListOfCharacters.Count - 1)].ToUpper();                        
                        else if (randomUpper > 50 && randomUpper <= 70)
                            walletPassword += ListOfSpecialCharacters[ClassUtils.GetRandomBetween(0, ListOfSpecialCharacters.Count - 1)];                        
                        else
                            walletPassword += ListOfNumbers[ClassUtils.GetRandomBetween(0, ListOfNumbers.Count - 1)];
                        
                    }
                }
            }
            return walletPassword;
        }

        /// <summary>
        /// Check if the word contain number(s)
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public static bool CheckNumber(string word)
        {
            return ListOfNumbers.Where((t, i) => i < ListOfNumbers.Count).Any(t => word.Contains(Convert.ToString(t)));
        }

        /// <summary>
        /// Check if the word contain letter(s)
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public static bool CheckLetter(string word)
        {
            return ListOfCharacters.Where((t, i) => i < ListOfCharacters.Count).Any(word.Contains);
        }

        /// <summary>
        /// Check if the word contain special character(s)
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public static bool CheckSpecialCharacters(string word)
        {
            return new Regex("[^a-zA-Z0-9_.]").IsMatch(word);
        }


        /// <summary>
        /// Get language filenames inside the list of language file.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="languageFolder"></param>
        /// <param name="languageFileList"></param>
        /// <returns></returns>
        public static List<string> GetLanguageFilenameListFromContentManager(ContentManager content, string languageFolder, string languageFileList)
        {
            List<string> result = new List<string>();

            using (var stream = TitleContainer.OpenStream(languageFolder + languageFileList))
            {
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
#if DEBUG
                        Debug.WriteLine("Language filename found: " + line);
#endif
                        result.Add(line);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get the text hidden by * character.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string GetTextHidden(string password)
        {
            string hiddenText = string.Empty;
            for(int i = 0; i < password.Length; i++)
                hiddenText += ClassConnectorSetting.PacketSplitSeperator;
            
            return hiddenText;
        }

        /// <summary>
        /// Copied from Xenophyte Connector All, fix index out of range on Gzip Decompress
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string DecompressData(string data)
        {
            using (MemoryStream input = new MemoryStream(Convert.FromBase64String(data)))
            {
                using (MemoryStream output = new MemoryStream())
                {
                    using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
                        dstream.CopyTo(output);
                    
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            }
        }

        #region Other functions

        public static string BytesToHex(byte[] bytes)
        {
            int startIndex = 0;
            int length = bytes.Length;
            int newSize = length * 3;
            char[] hexCharArray = new char[newSize];
            int currentIndex;
            for (currentIndex = 0; currentIndex < newSize; currentIndex += 3)
            {
                byte currentByte = bytes[startIndex++];
                hexCharArray[currentIndex] = GetHexValue(currentByte / 0x10);
                hexCharArray[currentIndex + 1] = GetHexValue(currentByte % 0x10);
                hexCharArray[currentIndex + 2] = '-';
            }
            return new string(hexCharArray, 0, hexCharArray.Length - 1).Replace("-", "");
        }

        /// <summary>
        /// Get Hex value from char index value.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private static char GetHexValue(int i)
        {
            return i < 10 ? (char)(i + 0x30) : (char)((i - 10) + 0x41);
        }

        /// <summary
        /// Get string between two strings.
        /// </summary>
        /// <param name="STR"></param>
        /// <param name="FirstString"></param>
        /// <param name="LastString"></param>
        /// <returns></returns>
        public static string GetStringBetween(string STR, string FirstString, string LastString)
        {
            string FinalString;
            int Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
            int Pos2 = STR.IndexOf(LastString);
            FinalString = STR.Substring(Pos1, Pos2 - Pos1);
            return FinalString;
        }

        public static string RemoveHTTPHeader(string packet)
        {
            return GetStringBetween(packet, "{", "}");
        }



        #endregion
    }

}