﻿using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
//using GHI.Glide;
//using GHI.Glide.Display;
using Json.NETMF;


namespace GadgeteerApp3
{
    public partial class Program
    {
        bool ledState = false;
        bool buttonState = false;
        GT.Timer timer;
        private string scannedRFID;
        private Boolean authInProgress = false;
        private Boolean networkUp = false;
        GT.Timer timeOutTimer = new GT.Timer(5000);
        private string webserverUrl = "http://192.168.1.2:8008/DEMOService/enter";
        Font fontNina = Resources.GetFont(Resources.FontResources.NinaB);

        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            displayText("Program Started");
            ethernetJ11D.UseStaticIP("192.168.1.202", "255.255.255.0", "192.168.1.2");
            ethernetJ11D.UseThisNetworkInterface();
            /*
            timer = new GT.Timer(300);
            timer.Tick += timer_tick;

            multicolorLED.TurnWhite();
            Thread.Sleep(1000);
            multicolorLED.TurnOff();

            //timer.Start();

            button.ButtonPressed += buttonPressed;

            displayTE35.SimpleGraphics.DisplayRectangle(GT.Color.Magenta, 2, GT.Color.Black, 1, 1, 100, 100);
            Thread bouncer = new Thread(BouncerLoop);
            //bouncer.Start();
            */
            /*Window window = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.TextFile1));

            GlideTouch.Initialize();

            Glide.MainWindow = window;*/

            rfidReader.IdReceived += rfidReader_IdReceived;
            camera.PictureCaptured += PictureCaptured;
            //displayTE35.WPFWindow.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(touch_down);

        
        }

        private void rfidReader_IdReceived(RFIDReader sender, string e)
        {
            if (authInProgress == false)
            {
                displayText("RFID scanned: " + e);
                scannedRFID = e;
                if (camera.CameraReady)
                {
                    authInProgress = true;
                    camera.TakePicture();
                    timeOutTimer.Start();
                }
                else
                {
                    displayText("Camera not ready");
                    /* cameraRetryTimer.Start();
                   cameraRetryCount = 0;
                    */
                }
            }
            else
            {
                displayText("Authentication already in progress");
            }
        }


        void PictureCaptured(Camera sender, GT.Picture picture) {
            displayText("Picture captured");
            if (authInProgress)
            {
                sendAuthRequest(scannedRFID, picture);
            }
            else
            {
               displayText("Rescan RFID");
            }
            /*Class1 class1 = new Class1();
            class1.MyProperty = 3;
            class1.image = Convert.ToBase64String(picture.MakeBitmap().GetBitmap());
            
            try
            {
                string json = JsonSerializer.SerializeObject(class1);
                Debug.Print("json: " + json);
            }
            catch
            {
                Debug.Print("serialize error");
            }*/

            //displayTE35.SimpleGraphics.Clear();
            //displayTE35.SimpleGraphics.DisplayImage(picture, 0, 0);
            //camera.StartStreaming();
        }

        private void sendAuthRequest(string scannedRFID, GT.Picture capturedImage)
        {
            /* Code for sending rfid and picture to webserver */
            if (ethernetJ11D.IsNetworkUp)
            {                
                string jsonString = getJsonString(scannedRFID, capturedImage);
                displayText("JSON string: " + jsonString);
                displayText("Network up. Trying to send authentication request..");
                
                POSTContent jsonContent = POSTContent.CreateTextBasedContent(jsonString);
                var req = HttpHelper.CreateHttpPostRequest(webserverUrl, jsonContent, "application/json");
                
                //var req = HttpHelper.CreateHttpGetRequest("http://192.168.1.2:8008/DEMOService/prova");
                req.ResponseReceived += new HttpRequest.ResponseHandler(req_ResponseReceived);
                req.SendRequest();
            }
            else
            {
                string jsonString = getJsonString(scannedRFID, capturedImage);
                displayText("JSON string: " + jsonString);
                displayText("Authentication failed because network is down");
            }

            authInProgress = false;
            timeOutTimer.Stop();
        }

        void req_ResponseReceived(HttpRequest sender, HttpResponse response)
        {
            displayText("Response received");
            displayText("Response status code: " + response.StatusCode + ", text: "+response.Text);
            if (response.StatusCode != "200")
            {
                displayText("Network down error");
                networkUp = false;
            }

            
        }

        string getJsonString(string scannedRFID, GT.Picture capturedImage)
        {
            /*Class1 class1 = new Class1();
            class1.MyProperty = 3;
            class1.image = Convert.ToBase64String(picture.MakeBitmap().GetBitmap());
            
            try
            {
                string json = JsonSerializer.SerializeObject(class1);
                Debug.Print("json: " + json);
            }
            catch
            {
                Debug.Print("serialize error");
            }*/
            //string jsonString = "";
            string json = "";
            Class1 testObj = new Class1();
            testObj.rfid = scannedRFID;
            byte[] imageByteArray = capturedImage.PictureData; //capturedImage.MakeBitmap().GetBitmap();
            Debug.Print("array length: " + imageByteArray.Length.ToString());
            try
            {
                testObj.photo = Convert.ToBase64String(imageByteArray); //"hsdaspjd324ji2p34j";
            }
            catch
            {
                displayText("Problem converting image to str");
            }
            try
            {              
                json = JsonSerializer.SerializeObject(testObj);
            }
            catch
            {
                displayText("serialize error");
            }
            //Debug.Print(json);
            return json;
            //return "";
        }


        void buttonPressed(Button sender, Button.ButtonState state)
        {
            /*buttonState = !buttonState;
            if (buttonState)
            {
                timer.Start();
            }
            else
            {
                timer.Stop();
            }*/

            if (camera.CameraReady)
            {
                camera.TakePicture();
            }
            
        }

        private void rfidReader_MalformedIdReceived(RFIDReader sender, EventArgs e)
        {
            displayText("Please rescan your card");
            
        }

        private void displayText(string text)
        {
            Debug.Print(text);
            //displayTE35.SimpleGraphics.Clear();
            //displayTE35.SimpleGraphics.DisplayText(text, fontNina, GT.Color.White, 10, 10);
        }

    }
}
