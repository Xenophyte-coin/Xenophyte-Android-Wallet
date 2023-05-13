using Android.Content;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using XenophyteAndroidWallet.Graphic;
using XenophyteAndroidWallet.Language;
using XenophyteAndroidWallet.User;
using XenophyteAndroidWallet.Wallet;
using Xenophyte_Connector_All.RPC;
using Xenophyte_Connector_All.Setting;
using Xenophyte_Connector_All.Utils;
using Xenophyte_Connector_All.Wallet;
using ZXing;
using ZXing.Mobile;
using ZXing.QrCode;

namespace XenophyteAndroidWallet.Event
{
    public class ClassGraphicEvent
    {
        public readonly string NoneEvent = "none";
        public Dictionary<string, Dictionary<string, string>> DictionaryEventTextTranslated = new Dictionary<string, Dictionary<string, string>>();
        private Interface _mainInterface;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="mainInterface"></param>
        public ClassGraphicEvent(Interface mainInterface)
        {
            _mainInterface = mainInterface;
        }

        /// <summary>
        /// Call a function from a graphic object name target by touch.
        /// </summary>
        /// <param name="graphicObjectName"></param>
        public async Task GraphicClickHandler(string graphicObjectName)
        {
            try
            {
                string eventName = _mainInterface.GraphicManager.GetGraphicEventName(graphicObjectName);

#if DEBUG
                Debug.WriteLine("Graphic Name: " + graphicObjectName + " | Test event name: " + eventName + " by click started..");
#endif

                if (eventName != NoneEvent)
                {

                    Type type = typeof(ClassGraphicEvent);
                    MethodInfo method = type.GetMethod(eventName);

                    if (method != null)
                    {


                        await (Task)method.Invoke(this, null);

#if DEBUG
                        Debug.WriteLine("Test event name: " + eventName + " by click successfully done.");
#endif
                    }
#if DEBUG
                    else
                        Debug.WriteLine("Event name: " + eventName + " not found.");
#endif
                }
            }
#if DEBUG
            catch (Exception error)
            {
                Debug.WriteLine("Error to execute an event: " + graphicObjectName + " | Exception: " + error.Message);
#else
            catch
            {
#endif
            }
        }

        /// <summary>
        /// Input the wallet password on the first initialization.
        /// </summary>
        /// <returns></returns>
        public async Task InputWalletPasswordOnWelcome()
        {
            string walletPassword = await EnableInputKeyboard(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordFirstInitializationTitleText), _mainInterface.LanguageObject.GetLanguageEventTextFormatted(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordFirstInitializationContentText), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, ClassConnectorSetting.WalletMinPasswordLength.ToString()), string.Empty, true);

            if (!string.IsNullOrEmpty(walletPassword))
            {
                if (walletPassword.Length < ClassConnectorSetting.WalletMinPasswordLength)
                {
                    await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordFirstInitializationErrorTitleText), _mainInterface.LanguageObject.GetLanguageEventTextFormatted(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordFirstInitializationErrorContentText), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, ClassConnectorSetting.WalletMinPasswordLength.ToString()), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                    await InputWalletPasswordOnWelcome();
                }
                else
                    ClassUserSetting.UserWalletPassword = walletPassword;
            }

#if DEBUG
            Debug.WriteLine("First Initialization - Wallet Password selected: " + ClassUserSetting.UserWalletPassword);
#endif
        }

        /// <summary>
        /// Open wallet file and continue to the main menu.
        /// </summary>
        /// <returns></returns>
        public async Task ButtonMenuPasswordOpenWallet()
        {
            if (ClassUserSetting.UserWalletPassword != null)
            {
                ClassUserSetting.WalletSystemBusy = true;
                if (_mainInterface.WalletDatabase.LoadWalletDatabase())
                {
                    await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletSuccessTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletSuccessContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                    ClassUserSetting.CurrentWalletAddressSelected = _mainInterface.WalletDatabase.AndroidWalletDatabase.ElementAt(0).Key;
                    BuildQrCodeTextureWalletAddress();
                    if (!_mainInterface.SyncDatabase.InitializeSyncDatabase())
                        _mainInterface.SyncDatabase.DatabaseTransactionSync.Clear();

                    _mainInterface.WalletUpdater.EnableAutoCheckSeedNodes();
                    _mainInterface.GraphicManager.SwitchMenu(ClassListMenu.MenuMain);
                }
                else
                {
                    var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseCancel), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                    if (result == 1)
                        await InputMenuPasswordOpenWallet();
                }
                ClassUserSetting.WalletSystemBusy = false;
            }

        }

        /// <summary>
        /// Input the wallet password on the menu open wallet.
        /// </summary>
        /// <returns></returns>
        public async Task InputMenuPasswordOpenWallet()
        {
            string walletPassword = await EnableInputKeyboard(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletContentText), string.Empty, true);
            if (walletPassword != null)
            {
                if (!string.IsNullOrEmpty(walletPassword))
                {
                    if (walletPassword.Length >= ClassConnectorSetting.WalletMinPasswordLength)
                    {
                        ClassUserSetting.UserWalletPassword = walletPassword;
                        ClassUserSetting.WalletSystemBusy = true;
                        if (_mainInterface.WalletDatabase.LoadWalletDatabase())
                            _mainInterface.WalletDatabase.AndroidWalletDatabase.Clear();
                        else
                        {
                            var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseCancel), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                            if (result == 1)
                                await InputMenuPasswordOpenWallet();
                        }
                    }
                    else
                    {
                        var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseCancel), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                        if (result == 1)
                            await InputMenuPasswordOpenWallet();
                    }
                }
                else
                {
                    var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseCancel), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                    if (result == 1)
                        await InputMenuPasswordOpenWallet();
                }
                ClassUserSetting.WalletSystemBusy = false;
            }
        }

        /// <summary>
        /// Create the first wallet on the first Initialization.
        /// </summary>
        public async Task ButtonWelcomeCreateWallet()
        {
            if (ClassUserSetting.WalletSystemBusy)
                await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CreateWalletBusyTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CreateWalletBusyContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
            else
            {
                if (!string.IsNullOrEmpty(ClassUserSetting.UserWalletPassword))
                {
                    if (ClassUserSetting.UserWalletPassword.Length < ClassConnectorSetting.WalletMinPasswordLength)
                    {
                        await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordFirstInitializationErrorTitleText), _mainInterface.LanguageObject.GetLanguageEventTextFormatted(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordFirstInitializationErrorContentText), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, ClassConnectorSetting.WalletMinPasswordLength.ToString()), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                        await InputWalletPasswordOnWelcome();
                    }
                    else
                        await CreateWallet(true);
                }
                else
                {
                    await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordFirstInitializationCreateWalletErrorTitleText), _mainInterface.LanguageObject.GetLanguageEventTextFormatted(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordFirstInitializationCreateWalletErrorContentText), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, ClassConnectorSetting.WalletMinPasswordLength.ToString()), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                    await InputWalletPasswordOnWelcome();
                }
            }
        }

        /// <summary>
        /// Open a webpage to browse the official website.
        /// </summary>

        /// <returns></returns>
        public async Task ButtonOfficialWebsite()
        {
            await ShowOfficialWebsite();
        }

        /// <summary>
        /// Input the wallet address to target on the transaction to send.
        /// </summary>

        /// <returns></returns>
        public async Task InputWalletAddressTargetSendTransaction()
        {
            string walletAddressTarget = await EnableInputKeyboard(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionSelectWalletAddressTargetTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionSelectWalletAddressTargetContentText), ClassUserSetting.CurrentWalletAddressTargetSendTransaction, false);

            if (walletAddressTarget != null)
            {
                if (walletAddressTarget.Length >= ClassConnectorSetting.MinWalletAddressSize && walletAddressTarget.Length <= ClassConnectorSetting.MaxWalletAddressSize)
                    ClassUserSetting.CurrentWalletAddressTargetSendTransaction = walletAddressTarget;
                else
                {
                    var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionSelectWalletAddressTargetErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionSelectWalletAddressTargetErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseNo), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseYes) });
                    if (result == 1)
                        await InputWalletAddressTargetSendTransaction();
                }
            }
        }

        /// <summary>
        /// Call a messagebox event and display the current wallet address used.
        /// </summary>
        /// <returns></returns>
        public async Task ButtonShowWalletAddress()
        {
            await DisplayCurrentWalletAddress();
        }

        /// <summary>
        /// Switch back to the main menu.
        /// </summary>
        /// <returns></returns>
        public void ButtonReturnToMainMenu()
        {
            _mainInterface.GraphicManager.SwitchMenu(ClassListMenu.MenuMain);
        }

        /// <summary>
        /// Switch to transaction menu.
        /// </summary>
        /// <returns></returns>
        public void ButtonSendTransaction()
        {
            _mainInterface.GraphicManager.SwitchMenu(ClassListMenu.MenuSendTransaction);
        }

        /// <summary>
        /// Permit to scan a qr code representing a wallet address.
        /// </summary>

        /// <returns></returns>
        public async Task ButtonScanQrCodeSendTransaction()
        {
            string qrCodeResult = await ScanQrCode();
            if (qrCodeResult != null)
            {
                if (qrCodeResult.Length >= ClassConnectorSetting.MinWalletAddressSize && qrCodeResult.Length <= ClassConnectorSetting.MaxWalletAddressSize)
                    ClassUserSetting.CurrentWalletAddressTargetSendTransaction = qrCodeResult;
                else
                {
                    var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionSelectWalletAddressTargetErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionSelectWalletAddressTargetErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseNo), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseYes) });
                    if (result == 1)
                        await ButtonScanQrCodeSendTransaction();
                }
            }
        }

        /// <summary>
        /// Input amount on sending transaction menu
        /// </summary>

        /// <returns></returns>
        public async Task InputAmountSelectedSendTransaction()
        {
            string formattedText = GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionAmountContentText);
            formattedText = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(formattedText, ClassLanguageSpecialCharacterEnumeration.AmountCharacter, _mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletBalance());
            formattedText = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(formattedText, ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, ClassConnectorSetting.CoinNameMin);

            string inputAmount = await EnableInputKeyboard(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionAmountTitleText), formattedText, ClassUserSetting.CurrentAmountSendTransaction.ToString(ClassUserSetting.GlobalCultureInfo).Replace(",", "."), false);

            if (inputAmount != null)
            {
                bool success = decimal.TryParse(inputAmount.Replace(".", ","), NumberStyles.Any, ClassUserSetting.GlobalCultureInfo, out var inputAmountParsed);
                if (success)
                {
                    decimal currentBalance = decimal.Parse(_mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletBalance().Replace(".", ","), NumberStyles.Any, ClassUserSetting.GlobalCultureInfo);
                    if (currentBalance >= inputAmountParsed)
                        ClassUserSetting.CurrentAmountSendTransaction = inputAmountParsed;
                    else
                        success = false;
                }
                if (!success)
                {
                    var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionAmountErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionAmountErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseNo), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseYes) });
                    if (result == 1)
                        await InputAmountSelectedSendTransaction();
                }
            }
        }

        /// <summary>
        /// Input fee on sending transaction menu
        /// </summary>

        /// <returns></returns>
        public async Task InputFeeSelectedSendTransaction()
        {
            string formattedText = GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionFeeContentText).Replace(".", ",");
            formattedText = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(formattedText, ClassLanguageSpecialCharacterEnumeration.AmountCharacter, _mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletBalance());
            formattedText = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(formattedText, ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, ClassConnectorSetting.CoinNameMin);
            string inputFee = await EnableInputKeyboard(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionFeeTitleText), formattedText, ClassUserSetting.CurrentFeeSendTransaction.ToString(ClassUserSetting.GlobalCultureInfo).Replace(".", ","), false);

            if (inputFee != null)
            {
                bool success = decimal.TryParse(inputFee.Replace(".", ","), NumberStyles.Any, ClassUserSetting.GlobalCultureInfo, out var inputFeeParsed);
                if (success)
                {
                    decimal currentBalance = decimal.Parse(_mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletBalance().Replace(".", ","), NumberStyles.Any, ClassUserSetting.GlobalCultureInfo);
                    if (currentBalance >= inputFeeParsed)
                    {
                        if (inputFeeParsed >= ClassConnectorSetting.MinimumWalletTransactionFee)
                            ClassUserSetting.CurrentFeeSendTransaction = inputFeeParsed;
                        else
                        {
                            string dialogErrorFee = GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionMandatoryFeeErrorContentText);
                            dialogErrorFee = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(dialogErrorFee, ClassLanguageSpecialCharacterEnumeration.FeeCharacter, ClassConnectorSetting.MinimumWalletTransactionFee.ToString(ClassUserSetting.GlobalCultureInfo).Replace(".", ","));
                            dialogErrorFee = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(dialogErrorFee, ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, ClassConnectorSetting.CoinNameMin);

                            var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionMandatoryFeeErrorTitleText), dialogErrorFee, new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseNo), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseYes) });
                            if (result == 1)
                                await InputFeeSelectedSendTransaction();
                        }
                    }
                    else
                        success = false;

                }
                if (!success)
                {
                    var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionFeeErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionFeeErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseNo), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseYes) });
                    if (result == 1)
                        await InputFeeSelectedSendTransaction();
                }
            }
        }

        /// <summary>
        /// Event button push a transaction to proceed to the network.
        /// </summary>
        /// <returns></returns>
        public async Task ButtonPushTransaction()
        {

            string pushTransactionContentFormatted = GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionPushContentText);
            pushTransactionContentFormatted = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(pushTransactionContentFormatted, ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, ClassUserSetting.CurrentWalletAddressTargetSendTransaction);
            pushTransactionContentFormatted = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(pushTransactionContentFormatted, ClassLanguageSpecialCharacterEnumeration.AmountCharacter, ClassUserSetting.CurrentAmountSendTransaction.ToString(ClassUserSetting.GlobalCultureInfo).Replace(",", ".") + " " + ClassConnectorSetting.CoinNameMin);
            pushTransactionContentFormatted = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(pushTransactionContentFormatted, ClassLanguageSpecialCharacterEnumeration.FeeCharacter, ClassUserSetting.CurrentFeeSendTransaction.ToString(ClassUserSetting.GlobalCultureInfo).Replace(",", ".") + " " + ClassConnectorSetting.CoinNameMin);

            var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionPushTitleText), pushTransactionContentFormatted, new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseNo), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseYes) });
            if (result == 1)
            {
                decimal currentBalance = decimal.Parse(_mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletBalance().Replace(".", ","), NumberStyles.Any, ClassUserSetting.GlobalCultureInfo);

                if (currentBalance >= ClassUserSetting.CurrentAmountSendTransaction + ClassUserSetting.CurrentFeeSendTransaction)
                {
                    if (await AskWalletPassword())
                    {
                        ClassUserSetting.WalletSystemBusy = true;
                        await Task.Factory.StartNew(async delegate ()
                        {

                            string transactionResult = await _mainInterface.WalletUpdater.ProceedTransactionTokenRequestAsync(ClassUserSetting.CurrentWalletAddressSelected, ClassUserSetting.CurrentAmountSendTransaction.ToString(ClassUserSetting.GlobalCultureInfo), ClassUserSetting.CurrentFeeSendTransaction.ToString(ClassUserSetting.GlobalCultureInfo), ClassUserSetting.CurrentWalletAddressTargetSendTransaction, false);
                            ClassUserSetting.WalletSystemBusy = false;

                            var splitTransactionResult = transactionResult.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);
                            switch (splitTransactionResult[0])
                            {
                                case ClassRpcWalletCommand.SendTokenTransactionRefused:
                                    await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionResultRefusedTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionResultRefusedContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                                    break;
                                case ClassRpcWalletCommand.SendTokenTransactionBusy:
                                    await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionResultBusyTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionResultBusyContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                                    break;
                                case ClassRpcWalletCommand.SendTokenTransactionInvalidTarget:
                                    await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionResultInvalidTargetTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionResultInvalidTargetContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                                    break;
                                case ClassRpcWalletCommand.SendTokenTransactionConfirmed:
                                    string transactionAcceptedDialogFormatted = GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionResultAcceptedContentText);
                                    transactionAcceptedDialogFormatted = _mainInterface.LanguageObject.GetLanguageEventTextFormatted(transactionAcceptedDialogFormatted, ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, splitTransactionResult[1].ToLower());
                                    await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionResultAcceptedTitleText), transactionAcceptedDialogFormatted, new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                                    break;
                            }

                        }).ConfigureAwait(false);
                    }
                }
                else
                {
                    await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionAmountErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.SendTransactionAmountErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                }
            }
        }

        /// <summary>
        /// Event button for switch into the transaction history menu.
        /// </summary>
        /// <returns></returns>
        public void ButtonTransactionHistory()
        {
            _mainInterface.GraphicManager.SwitchMenu(ClassListMenu.MenuTransactionHistory);
        }

        #region Other functions

        /// <summary>
        /// Create a wallet
        /// </summary>
        /// <param name="firstInitialization"></param>
        /// <returns></returns>
        public async Task CreateWallet(bool firstInitialization)
        {
            ClassUserSetting.WalletSystemBusy = true;
#if DEBUG
            Debug.WriteLine("Start to create the first wallet.");
#endif
            await Task.Factory.StartNew(async () =>
            {
                var walletCreatorObject = new ClassWalletCreator(_mainInterface);

                long dateCreateStart = DateTimeOffset.Now.ToUnixTimeMilliseconds() + ClassConnectorSetting.MaxTimeoutConnect;
                await Task.Factory.StartNew(async () =>
                {
                    if (!await walletCreatorObject.StartWalletConnectionAsync(ClassWalletPhase.Create, ClassUtility.MakeRandomWalletPassword()))
                    {
#if DEBUG
                        Debug.WriteLine("Android Wallet - Can't connect to the network.");
#endif
                        walletCreatorObject.WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                    }
                }, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).ConfigureAwait(false);


                while (walletCreatorObject.WalletCreateResult == ClassWalletCreatorEnumeration.WalletCreatorPending)
                {
                    await Task.Delay(100);
                    if (DateTimeOffset.Now.ToUnixTimeMilliseconds() >= dateCreateStart)
                    {
                        walletCreatorObject.WalletCreateResult = ClassWalletCreatorEnumeration.WalletCreatorError;
                        break;
                    }
                }

                switch (walletCreatorObject.WalletCreateResult)
                {
                    case ClassWalletCreatorEnumeration.WalletCreatorError:
                        await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CreateWalletErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CreateWalletErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
#if DEBUG
                        Debug.WriteLine("Android Wallet - Create a wallet error.");
#endif
                        break;
                    case ClassWalletCreatorEnumeration.WalletCreatorSuccess:
                        await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CreateWalletSuccessTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.CreateWalletSuccessContentText) + walletCreatorObject.WalletAddressResult, new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });

#if DEBUG
                        Debug.WriteLine("Android Wallet - successfully create a new wallet.");
                        Debug.WriteLine("New wallet address generated: " + walletCreatorObject.WalletAddressResult);
#endif
                        if (firstInitialization)
                        {
                            _mainInterface.GraphicManager.SwitchMenu(ClassListMenu.MenuMain);
                            if (!_mainInterface.SyncDatabase.InitializeSyncDatabase())
                                _mainInterface.SyncDatabase.DatabaseTransactionSync.Clear();

                        }
                        ClassUserSetting.CurrentWalletAddressSelected = walletCreatorObject.WalletAddressResult;
                        BuildQrCodeTextureWalletAddress();
                        break;
                }

                walletCreatorObject.FullDisconnection();


                ClassUserSetting.WalletSystemBusy = false;

            }, CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Current).ConfigureAwait(false);
        }

        /// <summary>
        /// Return the text of event selected.
        /// </summary>
        /// <param name="textType"></param>
        /// <returns></returns>
        public string GetTextEventFromLanguage(string textType)
        {
            return DictionaryEventTextTranslated[textType][_mainInterface.LanguageObject.CurrentLanguage];
        }

        /// <summary>
        /// Build the QR Code graphic texture from the current wallet address used.
        /// </summary>
        public void BuildQrCodeTextureWalletAddress()
        {
            QrCodeEncodingOptions options = new QrCodeEncodingOptions
            {
                DisableECI = true,
                CharacterSet = "UTF-8",
                Width = 256,
                Height = 256,
            };

            BarcodeWriter qr = new BarcodeWriter
            {
                Options = options,
                Format = BarcodeFormat.QR_CODE
            };
            using (var bitmapQrCode = qr.Write(ClassUserSetting.CurrentWalletAddressSelected))
            {
                _mainInterface.GraphicContent.WalletAddressQrCode = _mainInterface.GraphicManager.GetTexture(bitmapQrCode);
                double qrCodeCurrentWalletAddressWidth = (_mainInterface.GraphicManager.CurrentAndroidWalletWidth * 25) / 100;
                double qrCodeCurrentWalletAddressHeight = qrCodeCurrentWalletAddressWidth;

                double qrCodeCurrentWalletAddressPositionX = (_mainInterface.GraphicManager.CurrentAndroidWalletWidth - ((_mainInterface.GraphicManager.CurrentAndroidWalletWidth * 60) / 100));
                double qrCodeCurrentWalletAddressPositionY = ((_mainInterface.GraphicManager.CurrentAndroidWalletHeight * 0.5d) / 100);
                _mainInterface.GraphicContent.WalletAddressQrCodeRectangle = _mainInterface.GraphicManager.MakeRectangle(qrCodeCurrentWalletAddressPositionX, qrCodeCurrentWalletAddressPositionY, qrCodeCurrentWalletAddressWidth, qrCodeCurrentWalletAddressHeight);
            }
        }

        /// <summary>
        /// Permit to visit the official website. (Open default web browser).
        /// </summary>
        public async Task ShowOfficialWebsite()
        {
            var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.VisitOfficialWebsiteTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.VisitOfficialWebsiteContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseNo), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseYes) });
            if (result == 1)
            {
                var uri = Android.Net.Uri.Parse("https://xenophyte.com/");
                var intent = new Intent(Intent.ActionView, uri);
                _mainInterface.StartupObject.StartActivity(intent);
            }
        }

        /// <summary>
        /// Show birake exchange.
        /// </summary>
        /// <returns></returns>
        public void ButtonShowBirake()
        {

            var uri = Android.Net.Uri.Parse("https://trade.birake.com/market/BIRAKE.XENOP_BIRAKE.BTC");
            var intent = new Intent(Intent.ActionView, uri);
            _mainInterface.StartupObject.StartActivity(intent);

        }

        /// <summary>
        /// Show Xeggex exchange.
        /// </summary>
        public void ButtonShowXeggex()
        {

            var uri = Android.Net.Uri.Parse("https://xeggex.com/market/XENOP_BTC");
            var intent = new Intent(Intent.ActionView, uri);
            _mainInterface.StartupObject.StartActivity(intent);

        }

        /// <summary>
        /// Show caldera trade.
        /// </summary>
        public void ButtonShowCaldera()
        {
            var uri = Android.Net.Uri.Parse("https://caldera.trade/");
            var intent = new Intent(Intent.ActionView, uri);
            _mainInterface.StartupObject.StartActivity(intent);
        }

        /// <summary>
        /// Switch to exchange menu.
        /// </summary>
        /// <returns></returns>
        public void ButtonExchangeList()
        {
            _mainInterface.CurrentMenu = ClassListMenu.MenuExchange;
        }

        /// <summary>
        /// Display by message box the current wallet address used.
        /// </summary>
        /// <returns></returns>
        public async Task DisplayCurrentWalletAddress()
        {
            var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.ShowWalletAddressTitleText), _mainInterface.LanguageObject.GetLanguageEventTextFormatted(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.ShowWalletAddressContentText), ClassLanguageSpecialCharacterEnumeration.ReplaceCharacter, ClassUserSetting.CurrentWalletAddressSelected), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseNo), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseYes) });
            if (result == 1)
                Plugin.Clipboard.CrossClipboard.Current.SetText(ClassUserSetting.CurrentWalletAddressSelected);
        }

        /// <summary>
        /// Scan QR Code with the Camera, return the representation text of the QR Code scanned.
        /// </summary>
        /// <returns></returns>
        public async Task<string> ScanQrCode()
        {
            return await _mainInterface.StartupObject.StartQrCodeScanner();
        }

        /// <summary>
        /// Enable input Keyboard
        /// </summary>
        /// <param name="title"></param>
        /// <param name="contentText"></param>
        /// <param name="defaultText"></param>
        /// <param name="isPassword"></param>
        /// <returns></returns>
        public async Task<string> EnableInputKeyboard(string title, string contentText, string defaultText, bool isPassword)
        {
            try
            {
                if (!KeyboardInput.IsVisible)
                    return await KeyboardInput.Show(title, contentText, defaultText, isPassword);
            }
            catch
            {
                return null;
            }
            return null;
        }

        /// <summary>
        /// Ask the wallet password to the user.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> AskWalletPassword()
        {
            string walletPassword = await EnableInputKeyboard(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletContentText), string.Empty, true);
            if (walletPassword != null)
            {
                if (!string.IsNullOrEmpty(walletPassword))
                {
                    if (walletPassword.Length >= ClassConnectorSetting.WalletMinPasswordLength)
                    {
                        if (ClassUserSetting.UserWalletPassword == walletPassword)
                        {
                            string walletData = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.RijndaelWallet, _mainInterface.WalletDatabase.AndroidWalletDatabase[ClassUserSetting.CurrentWalletAddressSelected].GetWalletContentEncryptedFile(), _mainInterface.WalletDatabase.GenerateWalletEncryptionKey(ClassUserSetting.UserWalletPassword), ClassWalletNetworkSetting.KeySize);
                            if (walletData != ClassAlgoErrorEnumeration.AlgoError)
                            {
                                var splitWalletData = walletData.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);
                                if (splitWalletData[0] == ClassUserSetting.CurrentWalletAddressSelected)
                                    return true;
                            }
                            else
                            {
                                var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseCancel), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                                if (result == 1)
                                    return await AskWalletPassword();
                            }
                        }
                        else
                        {
                            var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseCancel), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                            if (result == 1)
                                return await AskWalletPassword();
                        }
                    }
                    else
                    {
                        var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseCancel), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                        if (result == 1)
                            return await AskWalletPassword();
                    }
                }
                else
                {
                    var result = await MessageBox.Show(GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorTitleText), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.PasswordOpenWalletErrorContentText), new[] { GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseCancel), GetTextEventFromLanguage(ClassGraphicEventTextEnumeration.MessageBoxDialogChooseOk) });
                    if (result == 1)
                        return await AskWalletPassword();
                }
            }
            return false;
        }

        #endregion

        #region Transaction history menu function

        /// <summary>
        /// Switch to a previous transaction page.
        /// </summary>
        public void ButtonPreviousTransactionPage()
        {
            if (GraphicSetting.CurrentTransactionPage > 1)
                GraphicSetting.CurrentTransactionPage--;
        }

        /// <summary>
        /// Switch to the next transaction page.
        /// </summary>
        public void ButtonNextTransactionPage()
        {
            if (GraphicSetting.CanIncrementPage())
                GraphicSetting.CurrentTransactionPage++;
        }


        #endregion
    }
}