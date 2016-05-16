using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
using GHI.Glide;
using GHI.Glide.Display;
using GHI.Glide.UI;
using Json.NETMF;
using System.Net.Sockets;
using System.Net;

namespace GadgeteerApp3
{
    public partial class Program
    {
        bool waitingForRfid = false;
        bool OkPressed = false;
        int currenRequestType = 0;  //0 - no request, 1 - enter, 2 - exit
        private string scannedRFID;
        private Boolean authInProgress = false;
        //private Boolean networkUp = false;
        GT.Timer timeOutTimer = new GT.Timer(15000);
        private string webserverUrl;// = "http://192.168.1.2:8008/DEMOService/enter";
        Font fontNina = Resources.GetFont(Resources.FontResources.NinaB);

        private static Window mainWindow;
        private static TextBlock txtNetworkStatus;
        private static TextBlock txtScreen;
        private static GHI.Glide.UI.Button btnEnter;
        private static GHI.Glide.UI.Button btnExit;
        private static GHI.Glide.UI.Button btnOk;
        private static ProgressBar barProgress;
        private static GHI.Glide.UI.Image imgPhoto;

        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            Debug.Print("Program Started");
            mainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.mainWindowXML));

            ethernetJ11D.UseStaticIP("192.168.1.202", "255.255.255.0", "192.168.1.2");
            ethernetJ11D.UseThisNetworkInterface();
            ethernetJ11D.NetworkDown += ethernetJ11D_NetworkDown;
            ethernetJ11D.NetworkUp += ethernetJ11D_NetworkUp;

            GlideTouch.Initialize();

            initWindow();

            Glide.MainWindow = mainWindow;

            rfidReader.IdReceived += rfidReader_IdReceived;
            camera.PictureCaptured += PictureCaptured;
            camera.CurrentPictureResolution = Camera.PictureResolution.Resolution320x240;
            camera.TakePictureStreamTimeout = new System.TimeSpan(0, 0, 0);
            timeOutTimer.Tick += timeOutTimer_Tick;
            camera.BitmapStreamed += camera_BitmapStreamed;

        
        }


        void initWindow()
        {
            btnEnter = (GHI.Glide.UI.Button)mainWindow.GetChildByName("btnEnter");
            btnExit = (GHI.Glide.UI.Button)mainWindow.GetChildByName("btnExit");
            btnOk = (GHI.Glide.UI.Button)mainWindow.GetChildByName("btnOk");
            txtNetworkStatus = (GHI.Glide.UI.TextBlock)mainWindow.GetChildByName("txtNetworkStatus");
            txtScreen = (GHI.Glide.UI.TextBlock)mainWindow.GetChildByName("txtText");
            barProgress = (GHI.Glide.UI.ProgressBar)mainWindow.GetChildByName("barRfidTime");
            imgPhoto = (GHI.Glide.UI.Image)mainWindow.GetChildByName("imgPhoto");
            imgPhoto.Stretch = true;

            displayDefaultScreen();

            btnEnter.TapEvent += btnEnter_TapEvent;
            btnExit.TapEvent += btnExit_TapEvent;
            btnOk.TapEvent += btnOk_TapEvent;


        }

        void displayDefaultScreen()
        {
            Debug.Print("displayDefaultScreen() called");
            currenRequestType = 0;
            multicolorLED.TurnOff();
            camera.StopStreaming();
            btnEnter.Visible = true;
            btnExit.Visible = true;



            btnOk.Visible = false;
            txtScreen.Visible = false;
            barProgress.Visible = false;
            imgPhoto.Visible = false;

            mainWindow.Invalidate();
        }


        void btnExit_TapEvent(object sender)
        {
            Debug.Print("btnExit pressed");
            currenRequestType = 2;
            startRfidWaiting();
        }

        void btnEnter_TapEvent(object sender)
        {
            Debug.Print("btnEnter pressed");
            currenRequestType = 1;
            startRfidWaiting();
        }

        void startRfidWaiting()
        {
            Debug.Print("startRfidWaiting() called");
            waitingForRfid = true;

            btnEnter.Visible = false;
            btnExit.Visible = false;


            btnOk.Visible = false;

            txtScreen.Visible = true;
            txtScreen.Text = "Align face and scan RFID";

            barProgress.Visible = true;
            barProgress.Value = 0;
            imgPhoto.Visible = true;
            imgPhoto.Alpha = 0;

            mainWindow.Invalidate();

            //camera.CurrentPictureResolution = Camera.PictureResolution.Resolution320x240;
            try
            {
                camera.StartStreaming();
            }
            catch
            {
                Debug.Print("Not able to start streaming");
            }


            Thread RfidThread = new Thread(RfidWait);
            RfidThread.Start();



        }

        void RfidWait(){
            int progressCount = 0;
            while ((progressCount <= 20) && (waitingForRfid == true))
            {

                barProgress.Value = progressCount * 5;
                barProgress.Invalidate();
                progressCount++;
                Thread.Sleep(500);
                
            }
            if (progressCount > 20)
            {
                cancelWaitingRfid();
                displayMessage("No RFID detected within time", true);

            }
                                  
        }

        void camera_BitmapStreamed(Camera sender, Bitmap e)
        {
            imgPhoto.Bitmap = e;
            imgPhoto.Alpha = 255;
            imgPhoto.Invalidate();
        }



        void displayMessage(string strMessage, bool OkEnabled)
        {
            Debug.Print("displayMessage() called");
            camera.StopStreaming();
            btnEnter.Visible = false;
            btnExit.Visible = false;
            barProgress.Visible = false;

            if (OkEnabled)
            {
                OkPressed = false;
                Thread OkThread = new Thread(OkWait);
                OkThread.Start();
            }

            txtScreen.Visible = true;
            txtScreen.Text = strMessage;

            mainWindow.Invalidate();
        }

        void OkWait()
        {
            int i = 5;
            //btnOk.Text = "OK (5sec)";
            btnOk.Visible = true;
            while (i > 0 && OkPressed == false)
            {
                btnOk.Text = "OK (" + i.ToString() + "sec)";
                btnOk.Invalidate();
                i--;
                Thread.Sleep(1000);
            }
            if (i <= 0)
            {
                displayDefaultScreen();
            }
            
        }

        void btnOk_TapEvent(object sender)
        {
            Debug.Print("btnOk pressed");
            OkPressed = true;
            displayDefaultScreen();
        }

        void cancelWaitingRfid()
        {
            Debug.Print("cancelWaitingRfid() called");
            waitingForRfid = false;
            camera.StopStreaming();
            barProgress.Visible = false;
            mainWindow.Invalidate();
            
            
        }


        void ethernetJ11D_NetworkUp(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            txtNetworkStatus.Text = "Network is up, IP: " + ethernetJ11D.NetworkSettings.IPAddress;
            txtNetworkStatus.Invalidate();
        }

        void ethernetJ11D_NetworkDown(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            txtNetworkStatus.Text = "Network is down";
            txtNetworkStatus.Invalidate();
        }

        void timeOutTimer_Tick(GT.Timer timer)
        {
            authInProgress = false;
            timeOutTimer.Stop();
            displayMessage("No response received within time", true);

        }

        private void rfidReader_IdReceived(RFIDReader sender, string e)
        {
            if (waitingForRfid)
            {
                cancelWaitingRfid();
                if (authInProgress == false)
                {
                    Debug.Print("RFID scanned: " + e);
                    scannedRFID = e;
                    if (camera.CameraReady)
                    {
                        displayMessage("RFID scanned, capturing photo..", false);
                        authInProgress = true;
                        //camera.CurrentPictureResolution = Camera.PictureResolution.Resolution320x240;
                        if (camera.CameraReady == false)
                        {
                            Debug.Print("Resolution changed and camera not ready");
                        }
                        camera.TakePicture();
                        timeOutTimer.Start();
                    }
                    else
                    {
                        Debug.Print("RFID received but camera not ready");
                    }
                }
                else
                {
                    Debug.Print("RFID received but authentication already in progress");
                }
               
            }
            else
            {
                Debug.Print("RFID received but no action selected");
            }
        }


        void PictureCaptured(Camera sender, GT.Picture picture)
        {
            Debug.Print("Picture captured");

            imgPhoto.Bitmap = picture.MakeBitmap();
            imgPhoto.Alpha = 255;
            imgPhoto.Invalidate();

            displayMessage("Picture captured, connecting to server..", false);

            if (authInProgress)
            {
                sendAuthRequest(scannedRFID, picture);
            }
            else
            {
                Debug.Print("Picture captured but no authentication in progress");
            }
        }

        private string getJsonString(string strRfid, string strSession){
            CJsonSerialize jsonObject = new CJsonSerialize();
            jsonObject.rfid = strRfid;
            jsonObject.session = strSession;
            string strJson = JsonSerializer.SerializeObject(jsonObject);
            return strJson;
        }

        private void sendAuthRequest(string scannedRFID, GT.Picture capturedImage)
        {
            /* Code for sending rfid and picture to webserver */
            if (ethernetJ11D.IsNetworkUp)
            {
                IPEndPoint ipep = new IPEndPoint(IPAddress.Parse("192.168.1.2"), 11000);
                Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                string hashSession = "";
                try
                {
                    clientSocket.Connect(ipep);
                    MySocketFunctions.socketSendFile(clientSocket, capturedImage.PictureData);
                    hashSession = MySocketFunctions.socketReadLine(clientSocket);
                    clientSocket.Close();
                    //authInProgress = false;

                    string jsonString = getJsonString(scannedRFID, hashSession);
                    Debug.Print("JSON string: " + jsonString);
                    //string jsonString = "{\"rfid\":\"" + scannedRFID + "\",\"session\":\"" + hashSession + "\"}";


                    POSTContent jsonContent = POSTContent.CreateTextBasedContent(jsonString);

                    if (currenRequestType == 1)
                    {
                        webserverUrl = "http://192.168.1.2:8008/DEMOService/enter";
                    }
                    else if (currenRequestType == 2)
                    {
                        webserverUrl = "http://192.168.1.2:8008/DEMOService/exit";
                    }
                    var req = HttpHelper.CreateHttpPostRequest(webserverUrl, jsonContent, "application/json");
                    //var req = HttpHelper.CreateHttpGetRequest("http://192.168.1.2:8008/DEMOService/prova");
                    req.ResponseReceived += new HttpRequest.ResponseHandler(req_ResponseReceived);
                    req.SendRequest();
                    Debug.Print("Request sended!");
                }
                catch
                {
                    Debug.Print("Socket error");

                }
            }
            else
            {
                Debug.Print("Authentication failed because network is down");
                displayMessage("Network down, check cable", true);
                authInProgress = false;
                timeOutTimer.Stop();
            }
            /*
            authInProgress = false;
            timeOutTimer.Stop();
             */
        }

        void handleResponseMessage(string strJson){
            //Debug.Print("Received response: " + strJson);
            Hashtable hashTable = JsonSerializer.DeserializeString(strJson) as Hashtable;
            string responseCode = hashTable["code"].ToString();
            //Debug.Print("response code: " + responseCode);
            string respnseMessage = hashTable["message"].ToString();
            
            if (currenRequestType == 1)
            {
                if (responseCode == "200") //entry of exit was success
                {
                    multicolorLED.TurnGreen();
                }
                else  //Error response received
                {
                    multicolorLED.TurnRed();
                }
                
            }
            
            displayMessage(respnseMessage, true);
        }


        void req_ResponseReceived(HttpRequest sender, HttpResponse response)
        {
            Debug.Print("Response received");
            if (authInProgress)
            {
              
                Debug.Print(response.Text);
                if (response.StatusCode == "200")
                {
                    handleResponseMessage(response.Text);
                }
                else
                {
                    Debug.Print("Error response, status code: "+response.StatusCode);
                    displayMessage("Response: " + response.Text, true);
                }
            }
            authInProgress = false;
            timeOutTimer.Stop();
            currenRequestType = 0;

        }


        private void rfidReader_MalformedIdReceived(RFIDReader sender, EventArgs e)
        {
            Debug.Print("Please rescan your card");
            
        }

    }
}
