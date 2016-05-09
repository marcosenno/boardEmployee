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

        private string scannedRFID;
        private Boolean authInProgress = false;
        //private Boolean networkUp = false;
        GT.Timer timeOutTimer = new GT.Timer(10000);
        private string webserverUrl = "http://192.168.1.2:8008/DEMOService/enter";
        Font fontNina = Resources.GetFont(Resources.FontResources.NinaB);

        private static Window mainWindow;
        private static TextBlock txtNetworkStatus;
        private static TextBlock txtScreen;
        private static GHI.Glide.UI.Button btnEnter;
        private static GHI.Glide.UI.Button btnExit;
        private static ProgressBar barProgress;

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

            timeOutTimer.Tick += timeOutTimer_Tick;
            //camera.BitmapStreamed += camera_BitmapStreamed;

            //displayTE35.WPFWindow.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(touch_down);

        
        }



        GT.Timer timerProgressBar = new GT.Timer(200);
        private int progressCount = 0;

        void initWindow()
        {
            btnEnter = (GHI.Glide.UI.Button)mainWindow.GetChildByName("btnEnter");
            btnExit = (GHI.Glide.UI.Button)mainWindow.GetChildByName("btnExit");
            txtNetworkStatus = (GHI.Glide.UI.TextBlock)mainWindow.GetChildByName("txtNetworkStatus");
            txtScreen = (GHI.Glide.UI.TextBlock)mainWindow.GetChildByName("txtText");
            barProgress = (GHI.Glide.UI.ProgressBar)mainWindow.GetChildByName("barRfidTime");

            barProgress.Visible = false;
            mainWindow.Graphics.DrawRectangle(barProgress.Rect, mainWindow.BackColor, 255);
            barProgress.Invalidate();

            changeScreenText("");

            btnEnter.TapEvent += btnEnter_TapEvent;
            btnExit.TapEvent += btnExit_TapEvent;

            timerProgressBar.Tick += timerProgressBar_Tick;
        }

        /*
        void camera_BitmapStreamed(Camera sender, Bitmap e)
        {
            displayTE35.SimpleGraphics.Clear();
            displayTE35.SimpleGraphics.DisplayImage(e, 0, 0);
        }
         */

        void btnExit_TapEvent(object sender)
        {
            waitingForRfid = true;
            changeButtonStates(false);
            progressCount = 0;
            timerProgressBar.Start();
        }

        void btnEnter_TapEvent(object sender)
        {
            waitingForRfid = true;
            changeButtonStates(false);
            progressCount = 0;
            timerProgressBar.Start();
        }

        void cancelWaitingRfid()
        {
            //changeButtonStates(true);
            waitingForRfid = false;
            timerProgressBar.Stop();
            mainWindow.Graphics.DrawRectangle(barProgress.Rect, mainWindow.BackColor, 255);
            barProgress.Visible = false;
            barProgress.Alpha = 0;
            barProgress.Invalidate();
            
            
        }

        void changeScreenText(string text)
        {
            txtScreen.Text = text;
            mainWindow.Graphics.DrawRectangle(txtScreen.Rect, mainWindow.BackColor, 255);
            txtScreen.Invalidate();
        }

        void timerProgressBar_Tick(GT.Timer timer)
        {
            if (progressCount <= 20)
            {
                barProgress.Value = progressCount * 5;
                barProgress.Visible = true;
                barProgress.Alpha = 255;
                barProgress.Invalidate();
                changeScreenText("Scan your RFID");
                progressCount++;

            }
            else
            {
                cancelWaitingRfid();
                changeButtonStates(true);
                changeScreenText("No RFID detected within time");
            }
        }

        void changeButtonStates(bool enabled)
        {
            btnEnter.Enabled = enabled;
            btnExit.Enabled = enabled;
            btnEnter.Invalidate();
            btnExit.Invalidate();
        }

        void ethernetJ11D_NetworkUp(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            txtNetworkStatus.Text = "Network is up, IP: " + ethernetJ11D.NetworkSettings.IPAddress;
            mainWindow.Graphics.DrawRectangle(txtNetworkStatus.Rect, mainWindow.BackColor, 255);
            txtNetworkStatus.Invalidate();
        }

        void ethernetJ11D_NetworkDown(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            txtNetworkStatus.Text = "Network is down";
            mainWindow.Graphics.DrawRectangle(txtNetworkStatus.Rect, mainWindow.BackColor, 255);
            txtNetworkStatus.Invalidate();
        }

        void timeOutTimer_Tick(GT.Timer timer)
        {
            authInProgress = false;
            timeOutTimer.Stop();
            changeButtonStates(true);
            changeScreenText("No resonse received within time");

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
                        changeScreenText("RFID scanned, connecting to server..");
                        authInProgress = true;
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
            /*
            displayTE35.SimpleGraphics.Clear();
            displayTE35.SimpleGraphics.DisplayImage(picture, 0, 0);
             */
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
                changeScreenText("Network down, check cable");
                changeButtonStates(true);
                authInProgress = false;
                timeOutTimer.Stop();
            }
            /*
            authInProgress = false;
            timeOutTimer.Stop();
             */
        }

        void handleResponseMessage(string strJson){
            Hashtable hashTable = JsonSerializer.DeserializeString(strJson) as Hashtable;
            //int responseCode = hashTable["code"];
            string respnseMessage = hashTable["message"].ToString();
            changeScreenText(respnseMessage);
        }

        void req_ResponseReceived(HttpRequest sender, HttpResponse response)
        {
            Debug.Print("Response received");
            if (authInProgress)
            {
                
                //changeScreenText(response.Text);
                //Debug.Print(response.Text);
                if (response.StatusCode == "200")
                {
                    handleResponseMessage(response.Text);
                    //networkUp = false;
                }
                else
                {
                    Debug.Print("Network down error");
                }
            }
            changeButtonStates(true);
            authInProgress = false;
            timeOutTimer.Stop();

        }


        private void rfidReader_MalformedIdReceived(RFIDReader sender, EventArgs e)
        {
            Debug.Print("Please rescan your card");
            
        }

    }
}
