using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Linq;
using Android.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using Newtonsoft.Json;
using XenophyteAndroidWallet.Language;
#if DEBUG
using System.Diagnostics;
#endif
using XenophyteAndroidWallet.Event;
using XenophyteAndroidWallet.Extension;
using XenophyteAndroidWallet.User;
using Xenophyte_Connector_All.Setting;
using Xenophyte_Connector_All.Utils;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;

namespace XenophyteAndroidWallet.Graphic
{
    public class GraphicContent
    {
        public Texture2D WalletAddressQrCode;
        public Rectangle WalletAddressQrCodeRectangle;
        public Texture2D ScrollBarCursor;
        public Rectangle ScrollBarCursorRectangle;
        public float ScrollBarCursorLastTotalTxForSize;
    }

    public class GraphicContentMandatoryDraw
    {
        public const string MandatoryDrawInputWelcomePasswordTextureName = "input-welcome-password-wallet-texture-name";
        public const string MandatoryDrawInputWalletAddressTargetSendTransactionTextureName = "input-wallet-address-target-send-transaction";
        public const string MandatoryDrawInputAmountSelectedSendTransactionTextureName = "input-amount-selected-send-transaction";
        public const string MandatoryDrawInputFeeSelectedSendTransactionTextureName = "input-fee-selected-send-transaction";
        public const string MandatoryDrawInputPasswordOpenWalletTextureName = "input-menu-password-open-wallet";
        public const string MandatoryDrawLoadingTextName = "loading-menu-animation-texture";
    }

    public class GraphicManager
    {

        private long MenuAnimationTimestampStart = ClassUtils.DateUnixTimeNowSecond();
        private const long AnimationElapsedTimeEnd = 9;
        private const long AnimationElapsedTimeLocationY = 6;
        private float AnimationRotationX = 360;
        private float AnimationLocationY = 0;

        private const string DefaultGraphicContentFolder = "Content/Graphics/";

        private const string DefaultGraphicDesignInfoPortraitFile = "Content/GraphicDesignInfoPortrait.json";
        //private const string DefaultGraphicDesignInfoLandscapeFile = "Content/GraphicDesignInfoLandscape.json";

        private Dictionary<string, GraphicObject> _listGraphicDesignInfoPortrait;

        /// <summary>
        /// Dictionnary allocated for store texture, texture name, location, menu type.
        /// </summary>
        private Dictionary<string, Tuple<string, Rectangle>> _dictionaryGraphics;
        private Dictionary<string, Tuple<string, Texture2D>> _dictionaryGraphicsTexture;


        /// <summary>
        /// About Texts.
        /// </summary>
        private SpriteFont _spriteFontArial;
        private Dictionary<string, Tuple<float, Vector2>> _dictionaryGraphicTexts; // Dictionary containing graphic name, { font size, vector2 }

        /// <summary>
        /// States
        /// </summary>
        public bool OnLoadGraphic;
        public int CurrentAndroidWalletWidth;
        public int CurrentAndroidWalletHeight;

        /// <summary>
        /// Object.
        /// </summary>
        private Interface _mainInterface;


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mainInterface"></param>

        public GraphicManager(Interface mainInterface)
        {
            _mainInterface = mainInterface;
            _listGraphicDesignInfoPortrait = new Dictionary<string, GraphicObject>();
            _dictionaryGraphicTexts = new Dictionary<string, Tuple<float, Vector2>>();
            _dictionaryGraphics = new Dictionary<string, Tuple<string, Rectangle>>();
            _dictionaryGraphicsTexture = new Dictionary<string, Tuple<string, Texture2D>>();
        }



        /// <summary>
        /// Load graphic design informations files.
        /// </summary>
        public void LoadGraphicDesignInfo()
        {
            _listGraphicDesignInfoPortrait.Clear();
            using (var stream = TitleContainer.OpenStream(DefaultGraphicDesignInfoPortraitFile))
            {
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!line.StartsWith("/") && !string.IsNullOrEmpty(line))
                        {
                            try
                            {
                                var graphicObject = JsonConvert.DeserializeObject<GraphicObject>(line);
                                if (!_listGraphicDesignInfoPortrait.ContainsKey(graphicObject.graphic_name))
                                    _listGraphicDesignInfoPortrait.Add(graphicObject.graphic_name, graphicObject);
#if DEBUG
                                else
                                    Debug.WriteLine(graphicObject.graphic_name + " graphic object, already exist.");
#endif
                            }
#if DEBUG
                            catch (Exception error)
                            {
                                Debug.WriteLine("Error on deserialize data line: " + line + " | Exception: " + error.Message);
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

        /// <summary>
        /// Load texture contained inside the android wallet
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="content"></param>
        /// <param name="reload"></param>
        public void LoadTextureContent(GraphicsDevice graphicsDevice, ContentManager content, bool reload = false)
        {
            OnLoadGraphic = true;
            try
            {
                if (!reload)
                {
                    _dictionaryGraphics.Clear();
                    _dictionaryGraphicsTexture.Clear();
                    _spriteFontArial = content.Load<SpriteFont>("Arial");
                }

                double positionX;
                double positionY;
                double width;
                double height;



                foreach (var graphicObject in _listGraphicDesignInfoPortrait)
                {

                    positionX = (_mainInterface.Graphics.GraphicsDevice.Viewport.Width * graphicObject.Value.percent_position_x) / 100;

                    positionY = (_mainInterface.Graphics.GraphicsDevice.Viewport.Height * graphicObject.Value.percent_position_y) / 100;
                    width = (_mainInterface.Graphics.GraphicsDevice.Viewport.Width * graphicObject.Value.percent_width) / 100;
                    height = (_mainInterface.Graphics.GraphicsDevice.Viewport.Height * graphicObject.Value.percent_height) / 100;

                    if (_dictionaryGraphics.ContainsKey(graphicObject.Value.graphic_name))
                    {
#if DEBUG
                        Debug.WriteLine("Update graphic object name: " + graphicObject.Value.graphic_name);
#endif
                        _dictionaryGraphics[graphicObject.Value.graphic_name] = new Tuple<string, Rectangle>(_dictionaryGraphics[graphicObject.Value.graphic_name].Item1, MakeRectangle(positionX, positionY, width, height));
                        LoadTextTextureContent(graphicObject.Value.graphic_name);
                    }
                    else
                    {
                        using (var stream = TitleContainer.OpenStream(DefaultGraphicContentFolder + graphicObject.Value.graphic_path_file))
                        {
#if DEBUG
                            Debug.WriteLine("Initialize graphic object name: " + graphicObject.Value.graphic_name);
#endif
                            InsertTexture(Texture2D.FromStream(graphicsDevice, stream), MakeRectangle(positionX, positionY, width, height), graphicObject.Value.graphic_menu_type, graphicObject.Value.graphic_name);
                            LoadTextTextureContent(graphicObject.Value.graphic_name);
                        }
                    }
                }

            }
            catch (Exception error)
            {
#if DEBUG
                Debug.WriteLine("LoadTextureContent Exception on Loading content: " + error.Message);
#endif
            }

            OnLoadGraphic = false;
        }

        /// <summary>
        /// Load or update current graphic text content of a texture object name.
        /// </summary>
        /// <param name="graphicName"></param>
        private void LoadTextTextureContent(string graphicName)
        {
            if (!_dictionaryGraphicTexts.ContainsKey(graphicName))
            {
                var languageObject = _mainInterface.LanguageObject.GetLanguageObjectFromKey(_mainInterface.LanguageObject.CurrentLanguage, graphicName);
                if (languageObject != null)
                {
                    var rectangle = GetRectangleFromTextureName(graphicName);
                    float languageObjectX = (float)(rectangle.X + ((rectangle.Width * languageObject.percent_position_x) / 100));
                    float languageObjectY = (float)(rectangle.Y + ((rectangle.Height * languageObject.percent_position_y) / 100));

                    float textSize = rectangle.Width / rectangle.Height * (rectangle.Width / rectangle.Height);
                    textSize *= 2;
                    _dictionaryGraphicTexts.Add(graphicName, new Tuple<float, Vector2>(textSize, new Vector2(languageObjectX, languageObjectY)));
                }
            }
            else
            {
                if (_dictionaryGraphicTexts[graphicName] != null)
                {
                    var languageObject = _mainInterface.LanguageObject.GetLanguageObjectFromKey(_mainInterface.LanguageObject.CurrentLanguage, graphicName);
                    if (languageObject != null)
                    {
                        var rectangle = GetRectangleFromTextureName(graphicName);
                        float languageObjectX = (float)(rectangle.X + ((rectangle.Width * languageObject.percent_position_x) / 100));
                        float languageObjectY = (float)(rectangle.Y + ((rectangle.Height * languageObject.percent_position_y) / 100));

                        float textSize = rectangle.Width / rectangle.Height * (rectangle.Width / rectangle.Height);
                        textSize *= 2;

                        _dictionaryGraphicTexts[graphicName] = new Tuple<float, Vector2>(textSize, new Vector2(languageObjectX, languageObjectY));
                    }
                }
            }
        }

        /// <summary>
        /// If the size change, the android wallet resize every element.
        /// </summary>
        public void AutoCheckCurrentViewportState(GraphicsDevice graphicsDevice, ContentManager content)
        {

            if (CurrentAndroidWalletWidth != _mainInterface.Graphics.GraphicsDevice.Viewport.Width || CurrentAndroidWalletHeight != _mainInterface.Graphics.GraphicsDevice.Viewport.Height)
            {
                if (!OnLoadGraphic)
                {
#if DEBUG
                    Debug.WriteLine("Viewport size change, reload graphic elements for resize them..");
#endif
                    LoadTextureContent(graphicsDevice, content, true);
                    CurrentAndroidWalletWidth = _mainInterface.Graphics.GraphicsDevice.Viewport.Width;
                    CurrentAndroidWalletHeight = _mainInterface.Graphics.GraphicsDevice.Viewport.Height;
#if DEBUG
                    Debug.WriteLine("Reload graphic elements and resize them successfully done.");
#endif
                }
            }

        }

        /// <summary>
        /// Insert texture with his rectangle (size)
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="rectangle"></param>
        /// <param name="menu"></param>
        /// <param name="textureName"></param>
        public void InsertTexture(Texture2D texture, Rectangle rectangle, string menu, string textureName)
        {
            if (!_dictionaryGraphics.ContainsKey(textureName) && !_dictionaryGraphicsTexture.ContainsKey(textureName))
            {
#if DEBUG
                Debug.WriteLine("New texture: " + textureName + " with Rectangle | Width: " + rectangle.Width + " Height: " + rectangle.Height + " X: " + rectangle.X + " Y: " + rectangle.Y + " | Menu: " + menu);
#endif
                _dictionaryGraphics.Add(textureName, new Tuple<string, Rectangle>(menu, rectangle));
                _dictionaryGraphicsTexture.Add(textureName, new Tuple<string, Texture2D>(menu, texture));
            }
        }

        /// <summary>
        /// Return a valid rectangle of texture if the texture name exist.
        /// </summary>
        /// <param name="textureName"></param>
        /// <returns></returns>
        public Rectangle GetRectangleFromTextureName(string textureName)
        {
            if (_dictionaryGraphics.ContainsKey(textureName))
                return _dictionaryGraphics[textureName].Item2;

            return new Rectangle();
        }

        /// <summary>
        /// Return a rectangle done from a selected width, height, position X and Y.
        /// </summary>
        /// <param name="targetHeight"></param>
        /// <param name="targetWidth"></param>
        /// <param name="targetX"></param>
        /// <param name="targetY"></param>
        /// <returns></returns>
        public Rectangle MakeRectangle(double targetX, double targetY, double targetWidth, double targetHeight)
        {
            return new Rectangle(new Point((int)targetX, (int)targetY), new Point((int)targetWidth, (int)targetHeight));
        }



        /// <summary>
        /// Draw every textures with their rectangles.
        /// </summary>
        /// <param name="spriteBatchDraw"></param>
        /// <param name="transparencyColor"></param>
        public void DrawTexture(SpriteBatch spriteBatchDraw, Color transparencyColor)
        {

            spriteBatchDraw.Begin();

            try
            {

                if (_dictionaryGraphics.Count > 0)
                {
                    foreach (var graphicElement in _dictionaryGraphics.ToArray())
                    {
                        if (_dictionaryGraphicsTexture.ContainsKey(graphicElement.Key))
                        {
                            if (graphicElement.Value.Item1 == _mainInterface.CurrentMenu || graphicElement.Value.Item1 == ClassListMenu.MenuAll)
                            {
                                #region Animation menu.

                                if (_mainInterface.CurrentMenu == ClassListMenu.MenuAnimation)
                                {
                                    if (graphicElement.Value.Item1 == "animation") // Do rotation animation.
                                    {
                                        var origin = new Vector2(_dictionaryGraphicsTexture[graphicElement.Key].Item2.Width / 2f, _dictionaryGraphicsTexture[graphicElement.Key].Item2.Height / 2f);

                                        spriteBatchDraw.Draw(
                                            _dictionaryGraphicsTexture[graphicElement.Key].Item2,
                                            new Rectangle(graphicElement.Value.Item2.X,
                                            graphicElement.Value.Item2.Y - (int)AnimationLocationY,
                                            graphicElement.Value.Item2.Width,
                                            graphicElement.Value.Item2.Height), null, Color.White, AnimationRotationX, origin, SpriteEffects.None, 0f);
                                    }
                                    else
                                        spriteBatchDraw.Draw(_dictionaryGraphicsTexture[graphicElement.Key].Item2, graphicElement.Value.Item2, transparencyColor);

                                }

                                #endregion

                                #region Transaction History Menu.

                                else if (_mainInterface.CurrentMenu == ClassListMenu.MenuTransactionHistory)
                                {

                                    #region Transaction history drawing event(s).

                                    // Show previous button.
                                    if (graphicElement.Key == "button-previous-page-transaction-history"
                                         && GraphicSetting.CurrentTransactionPage > 1)
                                    {
                                        spriteBatchDraw.Draw(_dictionaryGraphicsTexture[graphicElement.Key].Item2, graphicElement.Value.Item2, transparencyColor);

                                        if (_dictionaryGraphicTexts.ContainsKey(graphicElement.Key))
                                            DrawTextureTextContent(spriteBatchDraw, graphicElement.Key);
                                    }
                                    else if (graphicElement.Key == "button-next-page-transaction-history" &&
                                        GraphicSetting.CanIncrementPage())
                                    {
                                        spriteBatchDraw.Draw(_dictionaryGraphicsTexture[graphicElement.Key].Item2, graphicElement.Value.Item2, transparencyColor);

                                        if (_dictionaryGraphicTexts.ContainsKey(graphicElement.Key))
                                            DrawTextureTextContent(spriteBatchDraw, graphicElement.Key);
                                    }
                                    else if (graphicElement.Key != "button-previous-page-transaction-history" &&
                                        graphicElement.Key != "button-next-page-transaction-history")
                                    {
                                        spriteBatchDraw.Draw(_dictionaryGraphicsTexture[graphicElement.Key].Item2, graphicElement.Value.Item2, transparencyColor);

                                        if (_dictionaryGraphicTexts.ContainsKey(graphicElement.Key))
                                            DrawTextureTextContent(spriteBatchDraw, graphicElement.Key);
                                    }

                                    #endregion

                                    #region Show transaction history per page.

                                    // Clean up previous data.
                                    GraphicSetting.ListTransactionPositionHistory.Clear();

                                    Rectangle rectangleDisplayWalletBalance = _dictionaryGraphics["big-text-transaction-history"].Item2;

                                    float scale = 1.25f + (float)rectangleDisplayWalletBalance.Height / rectangleDisplayWalletBalance.Width;

                                    var cloneListTransastionHistory = GraphicSetting.ListTransactionHistory.
                                        ToList()
                                        .OrderByDescending(x => x.Value.Timestamp)
                                        .ToList(); // Copied, ordered, listed.

                                    int countStart = (GraphicSetting.CurrentTransactionPage - 1) * GraphicSetting.MaxTransactionPerPage;
                                    int countEnd = GraphicSetting.CurrentTransactionPage * GraphicSetting.MaxTransactionPerPage;
                                    int countDraw = 0;

                                    for (int i = countStart; i < countEnd; i++)
                                    {
                                        if (i < cloneListTransastionHistory.Count ||
                                            countDraw >= GraphicSetting.MaxTransactionPerPage)
                                        {
                                            var transaction = cloneListTransastionHistory.ElementAt(i);

                                            float positionX = rectangleDisplayWalletBalance.X + (rectangleDisplayWalletBalance.X * 2.5f / 100);
                                            float height = rectangleDisplayWalletBalance.Height / GraphicSetting.MaxTransactionPerPage;
                                            float positionY = rectangleDisplayWalletBalance.Y + (height * countDraw);
                                            float percentX = rectangleDisplayWalletBalance.X + (rectangleDisplayWalletBalance.X * 10f / 100);


                                            Rectangle rectangle = new Rectangle((int)positionX, (int)positionY, rectangleDisplayWalletBalance.Width, (int)height);


                                            spriteBatchDraw.Draw(_dictionaryGraphicsTexture["big-text-welcome-waiting"].Item2, rectangle, Color.White);

                                            spriteBatchDraw.DrawString(_spriteFontArial, 
                                                transaction.Value.Amount + " | " + transaction.Value.Fee + " | " + transaction.Value.Date, 
                                                new Vector2(percentX, (rectangle.Y + rectangle.Height / 2)), 
                                                transaction.Value.IsSend ? Color.Red : Color.Green, 
                                                0,
                                                new Vector2(),
                                                scale, 
                                                SpriteEffects.None, 0);

                                            GraphicSetting.ListTransactionPositionHistory.TryAdd(transaction.Key, rectangle);
                                            countDraw++;
                                        }
                                    }

                                    // Clean up.
                                    cloneListTransastionHistory.Clear();

                                    #endregion
                                }
                                
                                #endregion
                                
                                #region Other menu(s) to draw.

                                else
                                {
                                    spriteBatchDraw.Draw(_dictionaryGraphicsTexture[graphicElement.Key].Item2, graphicElement.Value.Item2, transparencyColor);

                                    if (_dictionaryGraphicTexts.ContainsKey(graphicElement.Key))
                                        DrawTextureTextContent(spriteBatchDraw, graphicElement.Key);
                                }

                                #endregion
                            }
                        }
                    }
                }


                DrawMandatoryTexture(spriteBatchDraw, transparencyColor);
                DrawMandatoryText(spriteBatchDraw);
                DrawPopUpTexture(spriteBatchDraw, transparencyColor);


            }
#if DEBUG
            catch (Exception error)
            {
                Debug.WriteLine("Failed to draw texture. Exception:" + error.Message);
#else
            catch
            {
#endif
            }


            spriteBatchDraw.End();

            #region Ending animation.

            if (_mainInterface.CurrentMenu == ClassListMenu.MenuAnimation)
            {
                if (AnimationElapsedTimeEnd + MenuAnimationTimestampStart < ClassUtils.DateUnixTimeNowSecond())
                {

                    // If any wallet is found.
                    if (!_mainInterface.WalletDatabase.CheckWalletDatabase())
                        _mainInterface.CurrentMenu = ClassListMenu.MenuWelcome;
                    else
                        _mainInterface.CurrentMenu = ClassListMenu.MenuMainPassword;

                    _mainInterface.WalletUpdater.EnableAutoCheckSeedNodes();
                    _mainInterface.WalletUpdater.EnableAutoUpdateWallet();
                }
                #endregion
                #region Pending animation menu.
                else
                {
                    if (AnimationRotationX > 0)
                        AnimationRotationX -= 0.2f;

                    if (AnimationElapsedTimeLocationY + MenuAnimationTimestampStart <= ClassUtils.DateUnixTimeNowSecond())
                        AnimationLocationY += 3.5f;
                }
            }
            #endregion
        }

        /// <summary>
        /// Draw texture text content
        /// </summary>
        /// <param name="spriteBatchDraw"></param>
        /// <param name="graphicName"></param>
        private void DrawTextureTextContent(SpriteBatch spriteBatchDraw, string graphicName)
        {
            string graphicTextContent = _mainInterface.LanguageObject.GetLanguageTextFromKey(_mainInterface.LanguageObject.CurrentLanguage, graphicName);
            if (graphicTextContent.Contains(ClassLanguageSpecialCharacterEnumeration.NewLineCharacter))
            {
                float autoScale = _dictionaryGraphicTexts[graphicName].Item1 + ((float)_dictionaryGraphics[graphicName].Item2.Height / _dictionaryGraphics[graphicName].Item2.Width);
                WriteTooLongText(spriteBatchDraw, graphicTextContent, _dictionaryGraphicTexts[graphicName].Item2, _dictionaryGraphics[graphicName].Item2.Width, autoScale, Color.Black);
            }
            else
                DrawTextFeetToTexture2(spriteBatchDraw, graphicTextContent, GetRectangleFromTextureName(graphicName), Color.Black);
        }

        /// <summary>
        /// Draw mandatory popup texture.
        /// </summary>
        /// <param name="spriteBatchDraw"></param>
        /// <param name="transparencyColor"></param>
        private void DrawPopUpTexture(SpriteBatch spriteBatchDraw, Color transparencyColor)
        {
            if (ClassUserSetting.WalletSystemBusy)
            {
                if (_dictionaryGraphicsTexture.ContainsKey("big-text-welcome-waiting"))
                {
                    double width = (CurrentAndroidWalletWidth * 90.0d) / 100;
                    double height = (CurrentAndroidWalletHeight * 10.0d) / 100;

                    double positionY = ((CurrentAndroidWalletHeight * 50.0d) / 100) - height;
                    double positionX = (CurrentAndroidWalletWidth * 5.0d) / 100;


                    double scale = 3 + (_dictionaryGraphics["big-text-welcome-waiting"].Item2.Height / _dictionaryGraphics["big-text-welcome-waiting"].Item2.Width);


                    double positionTextX = positionX + (_spriteFontArial.MeasureString(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CustomMessageBoxWaitMoment)).X);
                    double positionTextY = positionY + (height / 2.5d);

                    spriteBatchDraw.Draw(_dictionaryGraphicsTexture["big-text-welcome-waiting"].Item2, MakeRectangle(positionX, positionY, width, height), transparencyColor);
                    spriteBatchDraw.DrawString(_spriteFontArial, _mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CustomMessageBoxWaitMoment), new Vector2((float)positionTextX, (float)positionTextY), Color.Black, 0.0f, Vector2.Zero, (float)scale, SpriteEffects.None, 0.0f);

                }
            }
        }

        /// <summary>
        /// Draw mandatory texture.
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="transparencyColor"></param>
        private void DrawMandatoryTexture(SpriteBatch spriteBatch, Color transparencyColor)
        {
            switch (_mainInterface.CurrentMenu)
            {
                case ClassListMenu.MenuSendTransaction:
                case ClassListMenu.MenuMain:
                case ClassListMenu.MenuTransactionHistory:
                    if (_mainInterface.GraphicContent.WalletAddressQrCode == null)
                        _mainInterface.GraphicEvent.BuildQrCodeTextureWalletAddress();
                    spriteBatch.Draw(_mainInterface.GraphicContent.WalletAddressQrCode, _mainInterface.GraphicContent.WalletAddressQrCodeRectangle, transparencyColor);
                    break;
            }
        }

        /// <summary>
        /// Enable mandatory text draw.
        /// </summary>
        /// <param name="spriteBatchDraw"></param>
        private void DrawMandatoryText(SpriteBatch spriteBatchDraw)
        {
            switch (_mainInterface.CurrentMenu)
            {
                case ClassListMenu.MenuAnimation:
                    {
                        float positionY = (_mainInterface.GraphicManager.CurrentAndroidWalletHeight * 70f) / 100f;
                        float scale = 1f + (float)_mainInterface.GraphicManager.CurrentAndroidWalletHeight / _mainInterface.GraphicManager.CurrentAndroidWalletWidth;

                        Vector2 sizeOfText = _spriteFontArial.MeasureString(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.LoadingMenuContentText));

                        float positionX = _mainInterface.GraphicManager.CurrentAndroidWalletWidth/2 - (sizeOfText.X * (scale/2));

                        spriteBatchDraw.DrawString(_spriteFontArial, _mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.LoadingMenuContentText), new Vector2(positionX, positionY), (AnimationElapsedTimeLocationY + MenuAnimationTimestampStart <= ClassUtils.DateUnixTimeNowSecond() ? Color.White : Color.AliceBlue), 0, new Vector2(), scale, SpriteEffects.None, 1);
                    }
                    break;
                case ClassListMenu.MenuWelcome:
                    {
                        if (!string.IsNullOrEmpty(ClassUserSetting.UserWalletPassword))
                            DrawTextFeetToTextureSmallerText(spriteBatchDraw, ClassUtility.GetTextHidden(ClassUserSetting.UserWalletPassword), GetRectangleFromTextureName(GraphicContentMandatoryDraw.MandatoryDrawInputWelcomePasswordTextureName), Color.Black);
                    }
                    break;
                case ClassListMenu.MenuMainPassword:
                    {
                        if (!string.IsNullOrEmpty(ClassUserSetting.UserWalletPassword))
                            DrawTextFeetToTextureSmallerText(spriteBatchDraw, ClassUtility.GetTextHidden(ClassUserSetting.UserWalletPassword), GetRectangleFromTextureName(GraphicContentMandatoryDraw.MandatoryDrawInputPasswordOpenWalletTextureName), Color.Black);
                    }
                    break;
                case ClassListMenu.MenuSendTransaction:
                    {
                        DrawTextFeetToTextureSmallerText(spriteBatchDraw, ClassUtils.FormatAmount(ClassUserSetting.CurrentAmountSendTransaction.ToString(ClassUserSetting.GlobalCultureInfo)).Replace(",", ".") + " " + ClassConnectorSetting.CoinNameMin, GetRectangleFromTextureName(GraphicContentMandatoryDraw.MandatoryDrawInputAmountSelectedSendTransactionTextureName), Color.Black);
                        DrawTextFeetToTextureSmallerText(spriteBatchDraw, ClassUtils.FormatAmount(ClassUserSetting.CurrentFeeSendTransaction.ToString(ClassUserSetting.GlobalCultureInfo)).Replace(",", ".") + " " + ClassConnectorSetting.CoinNameMin, GetRectangleFromTextureName(GraphicContentMandatoryDraw.MandatoryDrawInputFeeSelectedSendTransactionTextureName), Color.Black);

                        if (!string.IsNullOrEmpty(ClassUserSetting.CurrentWalletAddressTargetSendTransaction))
                        {
                            if (ClassUserSetting.CurrentWalletAddressTargetSendTransaction.Length > 0)
                                DrawTextFeetToTexture(spriteBatchDraw, ClassUserSetting.CurrentWalletAddressTargetSendTransaction, GetRectangleFromTextureName(GraphicContentMandatoryDraw.MandatoryDrawInputWalletAddressTargetSendTransactionTextureName), Color.Black);

                        }
                        if (!string.IsNullOrEmpty(ClassUserSetting.CurrentWalletAddressSelected))
                        {
                            Rectangle rectangleDisplayWalletBalance = _dictionaryGraphics["big-text-send-transaction"].Item2;

                            float scale = 0.75f + (float)rectangleDisplayWalletBalance.Height / rectangleDisplayWalletBalance.Width;

                            Vector2 currentWalletAddressVector = new Vector2(rectangleDisplayWalletBalance.X + ((rectangleDisplayWalletBalance.Width * 2.5f) / 100), rectangleDisplayWalletBalance.Y + ((rectangleDisplayWalletBalance.Height * 5) / 100));

                            string textWalletInformation;
                            if (_mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetLastWalletUpdateSuccess() >= DateTimeOffset.Now.ToUnixTimeSeconds())
                                textWalletInformation = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CurrentWalletActiveBalance), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, _mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletBalance()) + " " + ClassConnectorSetting.CoinNameMin + " " + ClassLanguageSpecialCharacterEnumeration.NewLineCharacter +
                                                                   _mainInterface.LanguageObject.GetLanguageEventTextFormatted(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CurrentWalletPendingBalance), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, _mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletPendingBalance()) + " " + ClassConnectorSetting.CoinNameMin;
                            else
                                textWalletInformation = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CurrentWalletActiveBalance), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, "Updating") + ClassLanguageSpecialCharacterEnumeration.NewLineCharacter +
                                                        _mainInterface.LanguageObject.GetLanguageEventTextFormatted(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CurrentWalletPendingBalance), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, "Updating");
                            WriteTooLongText(spriteBatchDraw, textWalletInformation, currentWalletAddressVector, rectangleDisplayWalletBalance.Width, scale, Color.Black);
                        }
                    }
                    break;
                case ClassListMenu.MenuMain:
                    {
                        if (!string.IsNullOrEmpty(ClassUserSetting.CurrentWalletAddressSelected))
                        {
                            Rectangle rectangleDisplayWalletBalance = _dictionaryGraphics["big-text-wallet-information"].Item2;

                            float scale = 1.5f + (float)rectangleDisplayWalletBalance.Height / rectangleDisplayWalletBalance.Width;

                            Vector2 currentWalletAddressVector = new Vector2(rectangleDisplayWalletBalance.X + ((rectangleDisplayWalletBalance.Width * 2.5f) / 100), rectangleDisplayWalletBalance.Y + ((rectangleDisplayWalletBalance.Height * 10) / 100));

                            string textWalletInformation;
                            if (_mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetLastWalletUpdateSuccess() >= DateTimeOffset.Now.ToUnixTimeSeconds())
                                textWalletInformation = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CurrentWalletActiveBalance), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, _mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletBalance()) + " " + ClassConnectorSetting.CoinNameMin + " " + ClassLanguageSpecialCharacterEnumeration.NewLineCharacter +
                                                                   _mainInterface.LanguageObject.GetLanguageEventTextFormatted(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CurrentWalletPendingBalance), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, _mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletPendingBalance()) + " " + ClassConnectorSetting.CoinNameMin;
                            else
                                textWalletInformation = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CurrentWalletActiveBalance), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, "Updating") + ClassLanguageSpecialCharacterEnumeration.NewLineCharacter +
                                                        _mainInterface.LanguageObject.GetLanguageEventTextFormatted(_mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CurrentWalletPendingBalance), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, "Updating");

                            WriteTooLongText(spriteBatchDraw, textWalletInformation, currentWalletAddressVector, rectangleDisplayWalletBalance.Width, scale, Color.Black);
                        }
                    }
                    break;

            }
        }


        #region Other functions related to graphic content

        /// <summary>
        /// Split multi-line character, cut text lines once this one is too long and create another line before to display the part cut.
        /// </summary>
        /// <param name="spriteBatchDraw"></param>
        /// <param name="graphicTextContent"></param>
        /// <param name="textLinePosition"></param>
        /// <param name="maxWidth"></param>
        /// <param name="autoScale"></param>
        /// <param name="textColor"></param>
        public void WriteTooLongText(SpriteBatch spriteBatchDraw, string graphicTextContent, Vector2 textLinePosition, float maxWidth, float autoScale, Color textColor)
        {
            if (graphicTextContent.Contains(ClassLanguageSpecialCharacterEnumeration.NewLineCharacter))
            {
                var splitGraphicTextContent = graphicTextContent.Split(new[] { ClassLanguageSpecialCharacterEnumeration.NewLineCharacter }, StringSplitOptions.None);

                if (splitGraphicTextContent.Length > 0)
                {
                    int numberLine = 0;
                    float textLineHeight = _spriteFontArial.MeasureString(splitGraphicTextContent[0]).Y * autoScale;
                    textLineHeight = textLineHeight + ((textLineHeight * 5) / 100);
                    foreach (var textLineObject in splitGraphicTextContent)
                    {
                        string textLineCopy = (string)textLineObject.Clone();
                        Vector2 textPosition = textLinePosition;
                        textPosition.Y = textLinePosition.Y + (textLineHeight * numberLine);
                        var textLineWidth = _spriteFontArial.MeasureString(textLineCopy).X * autoScale;
                        if (textLineWidth > maxWidth)
                        {
                            int totalLineToMake = (int)(textLineWidth / maxWidth);
                            float percentSubstring = (float)100 / totalLineToMake;
                            for (int i = 0; i < totalLineToMake; i++)
                            {

                                int start = (textLineCopy.Length * (int)(percentSubstring * i)) / 100;
                                int end = (textLineCopy.Length * (int)(percentSubstring * (i + 1))) / 100;

                                string textCutPart;
                                if (end >= textLineCopy.Length)
                                    textCutPart = textLineCopy.SafeSubstring(start, textLineCopy.Length - 1);
                                else
                                    textCutPart = textLineCopy.SafeSubstring(start, end);

                                spriteBatchDraw.DrawString(_spriteFontArial, textCutPart, textPosition, Color.Black, 0.0f, Vector2.Zero, autoScale, SpriteEffects.None, 0.0f);
                                numberLine++;
                                textPosition.Y = textLinePosition.Y + (textLineHeight * numberLine);
                            }
                        }
                        else
                        {
                            spriteBatchDraw.DrawString(_spriteFontArial, textLineCopy, textPosition, Color.Black, 0.0f, Vector2.Zero, autoScale, SpriteEffects.None, 0.0f);
                            numberLine++;
                        }
                    }
                }
                else
                    spriteBatchDraw.DrawString(_spriteFontArial, graphicTextContent, textLinePosition, Color.Black, 0.0f, Vector2.Zero, autoScale + 0.5f, SpriteEffects.None, 0.0f);

            }
            else
            {
                int numberLine = 0;
                float textLineHeight = _spriteFontArial.MeasureString(graphicTextContent).Y * autoScale;
                textLineHeight = textLineHeight + ((textLineHeight * 5) / 100);

                Vector2 textPosition = textLinePosition;
                textPosition.Y = textLinePosition.Y + (textLineHeight * numberLine);
                var textLineWidth = _spriteFontArial.MeasureString(graphicTextContent).X * autoScale;
                if (textLineWidth > maxWidth)
                {
                    int totalLineToMake = (int)(textLineWidth / maxWidth);
                    float percentSubstring = 100 / ((textLineWidth / maxWidth));
                    for (int i = 0; i < totalLineToMake; i++)
                    {
                        if (i < totalLineToMake)
                        {
                            string textLineClone = (string)graphicTextContent.Clone();
                            int start = (textLineClone.Length * (int)(percentSubstring * i)) / 100;
                            int end = (textLineClone.Length * (int)(percentSubstring * (i + 1))) / 100;

                            string textCutPart;
                            if (end >= textLineClone.Length)
                            {
                                end = textLineClone.Length - 1;
                                textCutPart = textLineClone.SafeSubstring(start, end);
                            }
                            else
                                textCutPart = textLineClone.SafeSubstring(start, end);

                            spriteBatchDraw.DrawString(_spriteFontArial, textCutPart, textPosition, Color.Black, 0.0f, Vector2.Zero, autoScale, SpriteEffects.None, 0.0f);
                            numberLine++;
                            textPosition.Y = textLinePosition.Y + (textLineHeight * numberLine);
                        }
                    }
                }
                else
                    spriteBatchDraw.DrawString(_spriteFontArial, graphicTextContent, textPosition, Color.Black, 0.0f, Vector2.Zero, autoScale + 0.5f, SpriteEffects.None, 0.0f);

            }
        }

        /// <summary>
        /// Permit to draw text linked to a texture propertly feet.
        /// </summary>
        /// <param name="spriteBatchDraw"></param>
        /// <param name="text"></param>
        /// <param name="rectangleTexture"></param>
        /// <param name="textColor"></param>
        public void DrawTextFeetToTexture(SpriteBatch spriteBatchDraw, string text, Rectangle rectangleTexture, Color textColor)
        {
            Rectangle textureRectangle = rectangleTexture;
            Vector2 size = _spriteFontArial.MeasureString(text);

            float xScale = (textureRectangle.Width / (size.X + ((size.X * 5f) / 100)));
            float yScale = (textureRectangle.Height / size.Y);

            float scale = Math.Min(xScale, yScale);

            int strWidth = (int)Math.Round(size.X * scale);
            int strHeight = (int)Math.Round(size.Y * scale);
            Vector2 position = new Vector2
            {
                X = (((textureRectangle.Width - strWidth) / 2) + textureRectangle.X),
                Y = (((textureRectangle.Height - strHeight) / 2) + textureRectangle.Y)
            };

            float rotation = 0.0f;
            Vector2 spriteOrigin = new Vector2(0, 0);
            float spriteLayer = 0.0f;
            SpriteEffects spriteEffects = new SpriteEffects();

            spriteBatchDraw.DrawString(_spriteFontArial, text, position, textColor, rotation, spriteOrigin, scale, spriteEffects, spriteLayer);
        }

        /// <summary>
        /// Permit to draw text linked to a button or other large texture propertly feet.
        /// </summary>
        /// <param name="spriteBatchDraw"></param>
        /// <param name="text"></param>
        /// <param name="rectangleTexture"></param>
        /// <param name="textColor"></param>
        public void DrawTextFeetToTexture2(SpriteBatch spriteBatchDraw, string text, Rectangle rectangleTexture, Color textColor)
        {
            Rectangle textureRectangle = rectangleTexture;
            Vector2 size = _spriteFontArial.MeasureString(text);

            float xScale = (textureRectangle.Width / (size.X * 2));
            float yScale = (textureRectangle.Height / (size.Y * 2));

            float scale = Math.Min(xScale, yScale);

            int strWidth = (int)Math.Round(size.X * scale);
            int strHeight = (int)Math.Round(size.Y * scale);
            Vector2 position = new Vector2
            {
                X = (((textureRectangle.Width - strWidth) / 2) + textureRectangle.X),
                Y = (((textureRectangle.Height - strHeight) / 2) + textureRectangle.Y)
            };

            float rotation = 0.0f;
            Vector2 spriteOrigin = new Vector2(0, 0);
            float spriteLayer = 0.0f;
            SpriteEffects spriteEffects = new SpriteEffects();

            spriteBatchDraw.DrawString(_spriteFontArial, text, position, textColor, rotation, spriteOrigin, scale, spriteEffects, spriteLayer);
        }

        /// <summary>
        /// Permit to draw text linked to a button or other large texture to get smaller text propertly feet.
        /// </summary>
        /// <param name="spriteBatchDraw"></param>
        /// <param name="text"></param>
        /// <param name="rectangleTexture"></param>
        /// <param name="textColor"></param>
        public void DrawTextFeetToTextureSmallerText(SpriteBatch spriteBatchDraw, string text, Rectangle rectangleTexture, Color textColor)
        {
            Rectangle textureRectangle = rectangleTexture;
            Vector2 size = _spriteFontArial.MeasureString(text);

            float xScale = (textureRectangle.Width / (size.X * 3));
            float yScale = (textureRectangle.Height / (size.Y * 3));

            float scale = Math.Min(xScale, yScale);

            int strWidth = (int)Math.Round(size.X * scale);
            int strHeight = (int)Math.Round(size.Y * scale);
            Vector2 position = new Vector2
            {
                X = (((textureRectangle.Width - strWidth) / 2) + textureRectangle.X),
                Y = (((textureRectangle.Height - strHeight) / 2) + textureRectangle.Y)
            };

            float rotation = 0.0f;
            Vector2 spriteOrigin = new Vector2(0, 0);
            float spriteLayer = 0.0f;
            SpriteEffects spriteEffects = new SpriteEffects();

            spriteBatchDraw.DrawString(_spriteFontArial, text, position, textColor, rotation, spriteOrigin, scale, spriteEffects, spriteLayer);
        }

        /// <summary>
        /// Handle touch/click event on graphic element.
        /// </summary>
        /// <param name="currentMenu"></param>
        public async Task<bool> EnableTouchPadGraphicEventAsync(string currentMenu)
        {

            bool touchPressed = false;
            Vector2 touchPressedLocation = new Vector2(0, 0);

            var touchPanelState = TouchPanel.GetState();

            foreach (var touchPanelObject in touchPanelState)
            {
                if (touchPanelObject.State == TouchLocationState.Pressed)
                {
                    touchPressed = true;
                    touchPressedLocation = touchPanelObject.Position;
#if DEBUG
                    Debug.WriteLine("TouchPad pressed, location get: " + touchPressedLocation.X + ";" + touchPressedLocation.Y);
#endif
                }
            }

            /*
            if (_mainInterface.CurrentMenu == ClassListMenu.MenuTransactionHistory)
            {

            }
            */

            if (touchPressed)
            {
                if (_dictionaryGraphics.Count > 0)
                {
                    foreach (var graphicElement in _dictionaryGraphics.ToArray())
                    {
                        if (_dictionaryGraphicsTexture.ContainsKey(graphicElement.Key))
                        {
                            if (graphicElement.Value.Item1 == currentMenu || graphicElement.Value.Item1 == ClassListMenu.MenuAll)
                            {
                                if (graphicElement.Value.Item2.Contains(touchPressedLocation))
                                {
#if DEBUG
                                    Debug.WriteLine("TouchPad pressed, start event from graphic object name: " + graphicElement.Key + ", location get: " + touchPressedLocation.X + ";" + touchPressedLocation.Y);
#endif
                                    await _mainInterface.GraphicEvent.GraphicClickHandler(graphicElement.Key);
                                }
                            }
                        }
                    }
                }

                #region Show a transaction depending the touch event position.

                if (_mainInterface.CurrentMenu == ClassListMenu.MenuTransactionHistory)
                {
                    if (GraphicSetting.ListTransactionPositionHistory.Count > 0)
                    {
                        foreach (var transactionElement in GraphicSetting.ListTransactionPositionHistory.ToArray())
                        {
                            if (transactionElement.Value.Contains(touchPressedLocation))
                            {
                                if (GraphicSetting.ListTransactionHistory.ContainsKey(transactionElement.Key))
                                {
                                    var transaction = GraphicSetting.ListTransactionHistory[transactionElement.Key];

                                    string message = 
                                        "Date: " + transaction.Date +
                                        "\nAmount: " + transaction.Amount +
                                        "\nFee: " + transaction.Fee +
                                        "\n" + (transaction.IsSend ? 
                                        _mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.TransactionHistorySendContentText) :
                                        _mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.TransactionHistoryReceivedContentText)) +
                                         transaction.WalletAddress +
                                        "\nHash: " + transaction.Hash;

                                    await MessageBox.Show("Transaction details:", message, new[] { _mainInterface.GraphicEvent.GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                                }
                            }
                        }
                    }
                }

                #endregion
            }

            return touchPressed;
        }

        /// <summary>
        /// Return the event name related to the graphic content name.
        /// </summary>
        /// <param name="graphicContentName"></param>
        /// <returns></returns>
        public string GetGraphicEventName(string graphicContentName)
        {
            if (_listGraphicDesignInfoPortrait.ContainsKey(graphicContentName))
                return _listGraphicDesignInfoPortrait[graphicContentName].graphic_event_name;

            return _mainInterface.GraphicEvent.NoneEvent;
        }

        /// <summary>
        /// Switch the menu to another menu target.
        /// </summary>
        /// <param name="menuTarget"></param>
        public void SwitchMenu(string menuTarget)
        {
            _mainInterface.CurrentMenu = menuTarget;
#if DEBUG
            Debug.WriteLine("Switch to menu type target: " + menuTarget);
#endif
        }

        /// <summary>
        /// Generate a texture from a Bitmap.
        /// </summary>
        /// <param name="targetBitmap"></param>
        /// <returns></returns>
        public Texture2D GetTexture(Bitmap targetBitmap)
        {
            return Texture2D.FromStream(_mainInterface.GraphicsDevice, targetBitmap);
        }

        /// <summary>
        /// Update the transaction history to draw.
        /// </summary>
        public void UpdateTransactionHistoryToDraw()
        {
            #region Build transaction history.

            GraphicSetting.ListTransactionHistory.Clear();

            foreach (var transaction in _mainInterface.SyncDatabase.DatabaseTransactionSync.OrderByDescending(x => x.Value))
            {
                string[] splitTransaction = transaction.Key.Split(new[] { "#" }, StringSplitOptions.None);

                bool isSent = splitTransaction[1] == "SEND";
                string hash = splitTransaction[2];
                decimal amount = decimal.Parse(splitTransaction[4].Replace(".", ","));
                decimal fee = decimal.Parse(splitTransaction[5].Replace(".", ","));
                string dateReceived = DateTimeOffset.FromUnixTimeSeconds(transaction.Value).DateTime.ToString();
                string target = splitTransaction[3];

                if (!GraphicSetting.ListTransactionHistory.ContainsKey(hash))
                {
                    GraphicSetting.ListTransactionHistory.TryAdd(hash, new TransactionGraphicObject()
                    {
                        Amount = amount,
                        Fee = fee,
                        Hash = hash,
                        Date = dateReceived,
                        IsSend = isSent,
                        Timestamp = transaction.Value,
                        WalletAddress = target
                    });
                }
            }

            #endregion

        }


        #endregion
    }
}