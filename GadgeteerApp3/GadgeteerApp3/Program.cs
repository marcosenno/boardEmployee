﻿using System;
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
using GHI.Glide.Geom;
using Json.NETMF;
using System.Net.Sockets;
using System.Net;

namespace GadgeteerApp3
{
    public partial class Program
    {
        bool waitingForRfid = false;        //if false and RFID is received then no action is taken
        bool OkPressed = false;
        int currentRequestType = 0;  //0 - no request, 1 - enter, 2 - exit
        private string scannedRFID;
        private Boolean authInProgress = false;
        //private Boolean networkUp = false;
        GT.Timer timeOutTimer = new GT.Timer(30000);        //timeout timer, started just before sending HttpReq. This time includes taking a picture and communicating with server
        private string webserverUrl;// = "http://192.168.1.2:8008/DEMOService/enter";
        Font fontNina = Resources.GetFont(Resources.FontResources.NinaB);

        private static Window mainWindow;
        private static TextBlock txtNetworkStatus;
        private static TextBlock txtScreen;
        private static TextBlock txtRectangle;
        private static GHI.Glide.UI.Button btnEnter;
        private static GHI.Glide.UI.Button btnExit;
        private static GHI.Glide.UI.Button btnOk;
        private static ProgressBar barProgress;
        private static GHI.Glide.UI.Image imgPhoto;

        private int TIMEOUT = 10000;

        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            //Debug.Print("Program Started");
            mainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.mainWindowXML));

            ethernetJ11D.UseStaticIP("192.168.1.202", "255.255.255.0", "192.168.1.2");
            ethernetJ11D.UseThisNetworkInterface();
            ethernetJ11D.NetworkDown += ethernetJ11D_NetworkDown;
            ethernetJ11D.NetworkUp += ethernetJ11D_NetworkUp;

            GlideTouch.Initialize();

            initWindow();   //initialize components in window

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
            txtRectangle = (GHI.Glide.UI.TextBlock)mainWindow.GetChildByName("txtRectangle");
            barProgress = (GHI.Glide.UI.ProgressBar)mainWindow.GetChildByName("barRfidTime");
            imgPhoto = (GHI.Glide.UI.Image)mainWindow.GetChildByName("imgPhoto");
            imgPhoto.Stretch = true;    //fits the 320x240 streaming picture in 160x120 image component on window
            displayDefaultScreen();

            btnEnter.TapEvent += btnEnter_TapEvent;
            btnExit.TapEvent += btnExit_TapEvent;
            btnOk.TapEvent += btnOk_TapEvent;
        }

        void displayDefaultScreen()     //display default screen with only ENTER and EXIT buttons
        {
            //Debug.Print("displayDefaultScreen() called");
            currentRequestType = 0;      //no active request
            multicolorLED.TurnOff();
            camera.StopStreaming();
            btnEnter.Visible = true;
            btnExit.Visible = true;

            btnOk.Visible = false;
            txtScreen.Visible = false;
            barProgress.Visible = false;
            imgPhoto.Visible = false;
            txtRectangle.Visible = false;

            mainWindow.Invalidate();
        }


        void btnExit_TapEvent(object sender)        // EXIT button pressed on screen
        {
            if (ethernetJ11D.IsNetworkUp)
            {
                //Debug.Print("btnExit pressed");
                currentRequestType = 2;      //2 - exit
                startRfidWaiting();
            }
            else
            {
                displayMessage("Unable to connect, check cable", true);
            }
        }

        void btnEnter_TapEvent(object sender)        // ENTER button pressed on screen
        {
            if (ethernetJ11D.IsNetworkUp)
            {
                //Debug.Print("btnEnter pressed");
                currentRequestType = 1;      //1 - enter
                startRfidWaiting();
            }
            else
            {
                displayMessage("Unable to connect, check cable", true);
            }
        }

        void startRfidWaiting()     //display screen with camerastream, text and progress bar waiting for RFID
        {
            //Debug.Print("startRfidWaiting() called");
            waitingForRfid = true;

            btnEnter.Visible = false;
            btnExit.Visible = false;

            btnOk.Visible = false;

            txtScreen.Visible = true;
            txtScreen.Text = "Align face and scan RFID";

            barProgress.Visible = true;
            barProgress.Value = 0;
            imgPhoto.Visible = true;
            imgPhoto.Alpha = 0;     //image component is set to transparent until camera stream is ready

            mainWindow.Invalidate();

            try
            {
                camera.StartStreaming();
            }
            catch
            {
                //Debug.Print("Not able to start streaming");
            }


            Thread RfidThread = new Thread(RfidWait);
            RfidThread.Start();     //start thread to update progress bar
        }

        void RfidWait()
        {
            int progressCount = 0;
            while ((progressCount <= 20) && (waitingForRfid == true))       //if RFID is read, waitingForRfid is set to "false" and thread exits
            {

                barProgress.Value = progressCount * 5;      //progress bar value can be 0-100
                barProgress.Invalidate();
                progressCount++;
                Thread.Sleep(1000);      //total waiting time = 20 * 1000ms = 20s

            }
            if ((progressCount > 20) && (waitingForRfid == true))       //if waiting time has passed then stop waiting and display message
            {
                cancelWaitingRfid();
                displayMessage("No RFID detected within time", true);
            }

        }

        void camera_BitmapStreamed(Camera sender, Bitmap e)
        {
            imgPhoto.Bitmap = e;
            imgPhoto.Alpha = 255;       //image component is set to non-transparent
            imgPhoto.Invalidate();
        }



        void displayMessage(string strMessage, bool OkEnabled)      //display message and optionally OK button with timer on the screen
        {
            //Debug.Print("displayMessage() called, message:" + strMessage);
            camera.StopStreaming();
            btnEnter.Visible = false;
            btnExit.Visible = false;
            barProgress.Visible = false;

            if (OkEnabled)
            {
                OkPressed = false;
                btnOk.Visible = true;
                Thread OkThread = new Thread(OkWait);
                OkThread.Start();
            }

            txtScreen.Visible = true;
            txtScreen.Text = strMessage;

            mainWindow.Invalidate();
        }

        void OkWait()       //thread for displaying and refreshing OK button with remaining time
        {
            int i = 5;      //time in seconds the user has to press OK button until default screen is automatically displayed
            while ((i > 0) && (OkPressed == false))     //if "OK" button is pressed, OkPressed is set to "true" and thread exits
            {
                btnOk.Text = "OK (" + i.ToString() + "sec)";
                btnOk.Invalidate();
                i--;
                Thread.Sleep(1000);     //sleep for 1 second
            }
            if ((i <= 0) && (OkPressed == false))       //if waiting time has passed and user hasn't pushed "OK" button
            {
                displayDefaultScreen();
            }

        }

        void btnOk_TapEvent(object sender)
        {
            //Debug.Print("btnOk pressed");
            OkPressed = true;
            displayDefaultScreen();
        }

        void cancelWaitingRfid()        //called when RFID is received or waiting time has passed
        {
            //Debug.Print("cancelWaitingRfid() called");
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

        void timeOutTimer_Tick(GT.Timer timer)      //called when specified time has elapsed
        {
            if (timeOutTimer.IsRunning)
            {
                displayMessage("No response received within time", true);
                authInProgress = false;
            }
            timeOutTimer.Stop();

        }

        private void rfidReader_IdReceived(RFIDReader sender, string e)
        {
            if (waitingForRfid)     //check whether user has initiated RFID readig process (pressed ENTER or EXIT button)
            {
                cancelWaitingRfid();
                if (authInProgress == false)        //check whether another authentication process is already in progress
                {
                    //Debug.Print("RFID scanned: " + e);
                    scannedRFID = e;
                    if (camera.CameraReady)
                    {
                        displayMessage("RFID scanned, capturing photo..", false);       //display message to user without OK button
                        authInProgress = true;
                        camera.TakePicture();
                    }
                    else
                    {
                        //Debug.Print("RFID received but camera not ready");
                    }
                }
                else
                {
                    //Debug.Print("RFID received but authentication already in progress");
                }

            }
            else
            {
                //Debug.Print("RFID received but no action selected");
            }
        }


        void PictureCaptured(Camera sender, GT.Picture picture)
        {
            //Debug.Print("Picture captured");

            /* Display captured photo on screen */
            imgPhoto.Bitmap = picture.MakeBitmap();
            imgPhoto.Alpha = 255;
            imgPhoto.Invalidate();

            if (authInProgress)
            {
                displayMessage("Picture captured, connecting to server..", false);
                sendAuthRequest(scannedRFID, picture);
            }
            else
            {
                //Debug.Print("Picture captured but no authentication in progress");
            }
        }

        private string getJsonString(string strRfid, string strSession)     //parses RFID ID and session hash into JSON string
        {
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
                string hashSession = "";

                IPEndPoint ipep = new IPEndPoint(IPAddress.Parse("192.168.1.2"), 11000);
                Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.ReceiveTimeout = TIMEOUT;
                clientSocket.SendTimeout = TIMEOUT;
                
                try
                {
                    clientSocket.Connect(ipep);
                    MySocketFunctions.socketSendFile(clientSocket, capturedImage.PictureData);
                    hashSession = MySocketFunctions.socketReadLine(clientSocket);       //reads the session hashstring received from server socket as response
                }
                catch (Exception e)
                {
                    displayMessage("Network down", true);
                    authInProgress = false;
                    return;
                }
                finally
                {
                    clientSocket.Close();
                }

                string jsonString = getJsonString(scannedRFID, hashSession); //parse user RFID and session hash into JSON string
                //Debug.Print("JSON string: " + jsonString);
                //string jsonString = "{\"rfid\":\"" + scannedRFID + "\",\"session\":\"" + hashSession + "\"}";

                POSTContent jsonContent = POSTContent.CreateTextBasedContent(jsonString);

                if (currentRequestType == 1)     //user had pushed ENTER
                {
                    webserverUrl = "http://192.168.1.2:8008/DEMOService/enter";
                }
                else if (currentRequestType == 2)        //user had pushed EXIT
                {
                    webserverUrl = "http://192.168.1.2:8008/DEMOService/exit";
                }
                var req = HttpHelper.CreateHttpPostRequest(webserverUrl, jsonContent, "application/json");
                req.ResponseReceived += new HttpRequest.ResponseHandler(req_ResponseReceived);
                timeOutTimer.Start();       //start timeout timer (30 sec)
                req.SendRequest();
                //Debug.Print("Request sent!");
            }
            else
            {
                //Debug.Print("Authentication failed because network is down");
                multicolorLED.TurnRed();
                displayMessage("Network down, check cable", true);
                authInProgress = false;
                //timeOutTimer.Stop();
            }
            /*
            authInProgress = false;
            timeOutTimer.Stop();
             */
        }

        void handleResponseMessage(string strJson)
        {

            //Debug.Print("handleResponseMessage called");
            Hashtable hashTable = JsonSerializer.DeserializeString(strJson) as Hashtable;
            string responseCode = hashTable["code"].ToString();
            string respnseMessage = hashTable["message"].ToString();


            if (responseCode == "200") //entry of exit was success
            {
                string color = hashTable["color"].ToString();
                multicolorLED.TurnGreen();
                displayColorBox(color);

            }
            else  //Error response received
            {
                multicolorLED.TurnRed();
            }

            displayMessage(respnseMessage, true);
        }

        void displayColorBox(string color)
        {
            if (color == "GREEN")
            {
                txtRectangle.BackColor = Colors.Green;
            }
            else if (color == "YELLOW")
            {
                txtRectangle.BackColor = Colors.Yellow;
            }
            else if (color == "RED")
            {
                txtRectangle.BackColor = Colors.Red;
            }
            txtRectangle.Visible = true;
            mainWindow.Invalidate();
            imgPhoto.Invalidate();

        }


        void req_ResponseReceived(HttpRequest sender, HttpResponse response)
        {
            //Debug.Print("Response received");;
            if (authInProgress)
            {

                //Debug.Print("Response text: " + response.Text);
                if (response.StatusCode == "200")
                {
                    handleResponseMessage(response.Text);
                }
                else
                {
                    //Debug.Print("Error response, status code: "+response.StatusCode);
                    displayMessage("Response: " + response.Text, true);
                }
            }
            authInProgress = false;
            timeOutTimer.Stop();
            currentRequestType = 0;

        }


        private void rfidReader_MalformedIdReceived(RFIDReader sender, EventArgs e)
        {
            //Debug.Print("Malformed ID received");

        }

    }
}
