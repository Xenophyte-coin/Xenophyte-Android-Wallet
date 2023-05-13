using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
#if DEBUG
using System.Diagnostics;
#endif
using System.IO;
using Android.Content.Res;

namespace XenophyteAndroidWallet.Language
{
    public class ClassLanguageSpecialCharacterEnumeration
    {
        public const string CommentCharacter = "/";
        public const string NewLineCharacter = "&";
        public const string ReplaceCharacter = "%r";
        public const string AmountCharacter = "%a";
        public const string FeeCharacter = "%f";
    }

    public class ClassLanguageObject
    {
        public string language_name;
        public string language_graphic_name;
        public string language_content;
        public double percent_position_x;
        public double percent_position_y;
    }

    public class ClassLanguageEventObject
    {
        public string language_name;
        public string language_event_enumeration_name;
        public string language_event_content_text;
    }

    public class ClassLanguage
    {
        private const string DefaultLanguageFolderName = "Content/Language/";
        private const string DefaultLanguageFilelist = "LanguageFilelist.txt";
        private const string DefaultEventLanguageFilelist = "LanguageEventFilelist.txt";
        private const string DefaultLanguage = "EN";
        public  string CurrentLanguage;
        public  Dictionary<string, List<ClassLanguageObject>> LanguageDatabases = new Dictionary<string, List<ClassLanguageObject>>(); // Dictionnary content format -> {string:language name|List<ClassLanguageObject>: text content}

        private Interface _mainInterface;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mainInterface"></param>
        public ClassLanguage(Interface mainInterface)
        {
            _mainInterface = mainInterface;
        }

        /// <summary>
        /// Load language files.
        /// </summary>
        /// <param name="content"></param>
        public void LoadLanguage(ContentManager content)
        {
            //CurrentLanguage = DefaultLanguage;
            var listOfLanguageFile = ClassUtility.GetLanguageFilenameListFromContentManager(content, DefaultLanguageFolderName, DefaultLanguageFilelist);
            if (listOfLanguageFile.Count > 0)
            {
#if DEBUG
                Debug.WriteLine("Total language file found: " + listOfLanguageFile.Count);
#endif
                foreach(var languageFileName in listOfLanguageFile)
                {
                    if (languageFileName != null)
                    {
                        if (!string.IsNullOrEmpty(languageFileName))
                        {
#if DEBUG
                            Debug.WriteLine("Load language filename: " + languageFileName);
#endif
                            using (var stream = TitleContainer.OpenStream(DefaultLanguageFolderName+languageFileName))
                            {
                                using (var reader = new StreamReader(stream))
                                {
                                    string line;
                                    while ((line = reader.ReadLine()) != null)
                                    {
                                        if (!line.StartsWith(ClassLanguageSpecialCharacterEnumeration.CommentCharacter) && !string.IsNullOrEmpty(line))
                                        {
#if DEBUG
                                            Debug.WriteLine("Line read from language filename: " + languageFileName + " | content: " + line);
#endif
                                            var languageObject = JsonConvert.DeserializeObject<ClassLanguageObject>(line);
                                            if (!LanguageDatabases.ContainsKey(languageObject.language_name))
                                            {
                                                LanguageDatabases.Add(languageObject.language_name, new List<ClassLanguageObject>());
                                                LanguageDatabases[languageObject.language_name].Add(languageObject);
#if DEBUG
                                                Debug.WriteLine("Language name: " + languageObject.language_name + " loaded | Graphic Name: " + languageObject.language_content + " |  Content:  " + languageObject.language_content);
#endif
                                            }
                                            else
                                            {
                                                LanguageDatabases[languageObject.language_name].Add(languageObject);
#if DEBUG
                                                Debug.WriteLine("Language name: " + languageObject.language_name + " loaded | Graphic Name: " + languageObject.language_content + " |  Content:  " + languageObject.language_content);
#endif
                                            }
                                        }
#if DEBUG
                                        else
                                        {
                                            Debug.WriteLine("Comment line read: " + line + " ignored.");
                                        }
#endif
                                    }
                                }
                            }
                        }
                    }
                }
            }

            string currentLanguageName = Resources.System.Configuration.Locale.Country;

            CurrentLanguage = LanguageDatabases.ContainsKey(currentLanguageName) ? currentLanguageName : DefaultLanguage;
            //CurrentLanguage = DefaultLanguage;

        }

        /// <summary>
        /// Load event language files.
        /// </summary>
        /// <param name="content"></param>
        public void LoadEventLanguage(ContentManager content)
        {
            var listOfEventLanguageFile = ClassUtility.GetLanguageFilenameListFromContentManager(content, DefaultLanguageFolderName, DefaultEventLanguageFilelist);
            if (listOfEventLanguageFile.Count > 0)
            {
#if DEBUG
                Debug.WriteLine("Total event language file found: " + listOfEventLanguageFile.Count);
#endif
                foreach (var languageEventFileName in listOfEventLanguageFile)
                {
                    if (languageEventFileName != null)
                    {
                        if (!string.IsNullOrEmpty(languageEventFileName))
                        {
#if DEBUG
                            Debug.WriteLine("Load event language filename: " + languageEventFileName);
#endif
                            using (var stream = TitleContainer.OpenStream(DefaultLanguageFolderName + languageEventFileName))
                            {
                                using (var reader = new StreamReader(stream))
                                {
                                    string line;
                                    while ((line = reader.ReadLine()) != null)
                                    {
                                        try
                                        {
                                            if (!line.StartsWith(ClassLanguageSpecialCharacterEnumeration.CommentCharacter) && !string.IsNullOrEmpty(line))
                                            {
#if DEBUG
                                                Debug.WriteLine("Line read from event language filename: " + languageEventFileName + " | content: " + line);
#endif
                                                var languageEventObject = JsonConvert.DeserializeObject<ClassLanguageEventObject>(line);
                                                if (!_mainInterface.GraphicEvent.DictionaryEventTextTranslated.ContainsKey(languageEventObject.language_event_enumeration_name))
                                                {
                                                    _mainInterface.GraphicEvent.DictionaryEventTextTranslated.Add(languageEventObject.language_event_enumeration_name, new Dictionary<string, string>());
                                                    _mainInterface.GraphicEvent.DictionaryEventTextTranslated[languageEventObject.language_event_enumeration_name].Add(languageEventObject.language_name, languageEventObject.language_event_content_text);
#if DEBUG
                                                    Debug.WriteLine("Language name: " + languageEventObject.language_name + " loaded | Event Name: " + languageEventObject.language_event_enumeration_name + " |  Content:  " + languageEventObject.language_event_content_text);
#endif
                                                }
                                                else
                                                {
                                                    if (!_mainInterface.GraphicEvent.DictionaryEventTextTranslated[languageEventObject.language_event_enumeration_name].ContainsKey(languageEventObject.language_name))
                                                    {
                                                        _mainInterface.GraphicEvent.DictionaryEventTextTranslated[languageEventObject.language_event_enumeration_name].Add(languageEventObject.language_name, languageEventObject.language_event_content_text);
#if DEBUG
                                                        Debug.WriteLine("Language name: " + languageEventObject.language_name + " loaded | Event Name: " + languageEventObject.language_event_enumeration_name + " |  Content:  " + languageEventObject.language_event_content_text);
#endif
                                                    }
                                                }
                                            }
#if DEBUG
                                            else
                                            {
                                                Debug.WriteLine("Comment line read: " + line + " ignored.");
                                            }
#endif
                                        }
#if DEBUG
                                        catch(Exception error)
                                        {
                                            Debug.WriteLine("Failed to deserialize the line data: " + line + " |Exception: " + error.Message);
#else
                                        catch
                                        {
#endif
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            string currentLanguageName = Resources.System.Configuration.Locale.Country;

            CurrentLanguage = LanguageDatabases.ContainsKey(currentLanguageName) ? currentLanguageName : DefaultLanguage;

            //CurrentLanguage = DefaultLanguage;
        }


        /// <summary>
        /// Get language text from language name + graphic element name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="graphicName"></param>
        /// <returns></returns>
        public string GetLanguageTextFromKey(string key, string graphicName)
        {
            if (LanguageDatabases.ContainsKey(key))
            {
                foreach(var languageObject in LanguageDatabases[key])
                {
                    if (languageObject.language_graphic_name == graphicName)
                        return languageObject.language_content;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Get language text from language name + graphic element name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="graphicName"></param>
        /// <returns></returns>
        public  ClassLanguageObject GetLanguageObjectFromKey(string key, string graphicName)
        {
            if (LanguageDatabases.ContainsKey(key))
            {
                foreach (var languageObject in LanguageDatabases[key])
                {
                    if (languageObject.language_graphic_name == graphicName)
                        return languageObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the language event text target replaced by an element selected.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="targetReplace"></param>
        /// <param name="newText"></param>
        /// <returns></returns>
        public  string GetLanguageEventTextFormatted(string text, string targetReplace, string newText)
        {
            return text.Replace(targetReplace, newText);
        }
    }
}