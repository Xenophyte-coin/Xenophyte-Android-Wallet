using System;
using System.Diagnostics;
using System.IO;
using Android.Graphics;
using Android.Util;
using Xenophyte_Connector_All.Setting;
using Xenophyte_Connector_All.Utils;
using Xenophyte_Connector_All.Wallet;
using ZXing;
using ZXing.Common;
using ZXing.Mobile;
using ZXing.QrCode;

namespace XenophyteAndroidWallet.Wallet
{
    public class ClassWalletRestoreFunctions : IDisposable
    {
        /// <summary>
        /// Dispose information.
        /// </summary>
        private bool IsDisposed;

        #region Dispose functions

        ~ClassWalletRestoreFunctions()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
        }

        #endregion


        /// <summary>
        /// Generate QR Code from private key + password, encrypt the QR Code bitmap with the private key, build the request to be send on the blockchain.
        /// </summary>
        /// <param name="privateKey"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public string GenerateQrCodeKeyEncryptedRepresentation(string privateKey, string password)
        {
            try
            {
                QrCodeEncodingOptions options = new QrCodeEncodingOptions
                {
                    DisableECI = true,
                    CharacterSet = "UTF-8",
                    Width = 2,
                    Height = 2,
                };

                BarcodeWriter qr = new BarcodeWriter
                {
                    Options = options,
                    Format = BarcodeFormat.QR_CODE
                };
                string sourceKey = privateKey.Trim() + ClassConnectorSetting.PacketContentSeperator + password.Trim() + ClassConnectorSetting.PacketContentSeperator + DateTimeOffset.Now.ToUnixTimeSeconds();
                using (var representationQrCode = qr.Write(sourceKey))
                {

                    LuminanceSource source = new RGBLuminanceSource(BitmapToByteArray(representationQrCode), representationQrCode.Width, representationQrCode.Height);

                    BinaryBitmap bitmap = new BinaryBitmap(new HybridBinarizer(source));
                    Result result = new MultiFormatReader().decode(bitmap);

                    if (result != null)
                    {
                        if (result.Text == sourceKey)
                        {

                            string qrCodeString = BitmapToBase64String(representationQrCode);
                            string qrCodeStringEncrypted = ClassCustomAlgo.GetEncryptedResultManual(ClassAlgoEnumeration.RijndaelWallet, qrCodeString, privateKey, ClassWalletNetworkSetting.KeySize);
                            string qrCodeEncryptedRequest;

                            if (privateKey.Contains("$"))
                            {
                                long walletUniqueIdInstance = long.Parse(privateKey.Split(new[] { "$" }, StringSplitOptions.None)[1]);
                                qrCodeEncryptedRequest = walletUniqueIdInstance + ClassConnectorSetting.PacketContentSeperator + qrCodeStringEncrypted;
                            }
                            else
                            {

                                string randomEndPrivateKey = privateKey.Remove(0, (privateKey.Length - ClassUtils.GetRandomBetween(privateKey.Length / 4, privateKey.Length / 8))); // Indicate only a small part of the end of the private key (For old private key users).
                                qrCodeEncryptedRequest = randomEndPrivateKey + ClassConnectorSetting.PacketContentSeperator + qrCodeStringEncrypted;
                            }
                            string decryptQrCode = ClassCustomAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.RijndaelWallet, qrCodeStringEncrypted, privateKey, ClassWalletNetworkSetting.KeySize);

                            using (Bitmap qrCode = Base64StringToBitmap(decryptQrCode))
                            {

                                source = new RGBLuminanceSource(BitmapToByteArray(qrCode), qrCode.Width, qrCode.Height);

                                bitmap = new BinaryBitmap(new HybridBinarizer(source));
                                result = new MultiFormatReader().decode(bitmap);

                                if (result != null)
                                {
                                    if (result.Text == sourceKey)
                                    {
                                        return qrCodeEncryptedRequest;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception error)
            {
#if DEBUG
                Debug.WriteLine("error to generate qr code encryption, exception: " + error.Message);
#endif
            }
            return null;
        }

        /// <summary>
        /// Convert a bitmap into byte array then in base64 string.
        /// </summary>
        /// <param name="newImage"></param>
        /// <returns></returns>
        public string BitmapToBase64String(Bitmap newImage)
        {
            using (MemoryStream byteArrayOutputStream = new MemoryStream())
            {
                newImage.Compress(Bitmap.CompressFormat.Jpeg, 100, byteArrayOutputStream);
                return Base64.EncodeToString(byteArrayOutputStream.ToArray(), Base64Flags.Default);
            }
        }

        /// <summary>
        /// Convert Bitmap into ByteArray
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public byte[] BitmapToByteArray(Bitmap image)
        {
            using (MemoryStream byteArrayOutputStream = new MemoryStream())
            {
                image.Compress(Bitmap.CompressFormat.Jpeg, 100, byteArrayOutputStream);
                return byteArrayOutputStream.ToArray();
            }
        }

        /// <summary>
        /// Convert a base64 string into byte array, then into bitmap.
        /// </summary>
        /// <param name="stringImage"></param>
        /// <returns></returns>
        public Bitmap Base64StringToBitmap(string stringImage)
        {
            byte[] imageAsBytes = Base64.Decode(stringImage, Base64Flags.Default);
            return BitmapFactory.DecodeByteArray(imageAsBytes, 0, imageAsBytes.Length);
        }

    }

}