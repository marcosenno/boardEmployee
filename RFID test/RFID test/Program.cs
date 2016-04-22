using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;

using Json.NETMF;

namespace RFID_test
{
    public partial class Program
    {
        private string scannedRFID;
        private Boolean authInProgress = false;
        private Boolean networkUp = false;
        GT.Timer timeOutTimer = new GT.Timer(5000);
        private string webserverUrl = "localhost";

        Font fontNina = Resources.GetFont(Resources.FontResources.NinaB);
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            Debug.Print("Program Started");
            ethernetJ11D.UseStaticIP("192.168.1.202", "255.255.255.0", "192.168.1.201");
            ethernetJ11D.UseThisNetworkInterface();
            this.rfidReader.IdReceived += this.rfidReader_IdReceived;
            this.rfidReader.MalformedIdReceived += this.rfidReader_MalformedIdReceived;
            this.camera.PictureCaptured += camera_PictureCaptured;
        }

        void camera_PictureCaptured(Camera sender, GT.Picture e)
        {
            if (authInProgress)
            {
                sendAuthRequest(scannedRFID, e);
            }
            else
            {
                Debug.Print("Rescan RFID");
            }
        }

        private void sendAuthRequest(string scannedRFID, GT.Picture capturedImage)
        {
            /* Code for sending rfid and picture to webserver */
            if (true /*ethernetJ11D.IsNetworkUp*/)
            {
                string jsonString = getJsonString(scannedRFID, capturedImage);
                
                /*POSTContent jsonContent = POSTContent.CreateTextBasedContent(jsonString);
                var req = HttpHelper.CreateHttpPostRequest(webserverUrl, jsonContent, "application/json");
                req.ResponseReceived += new HttpRequest.ResponseHandler(req_ResponseReceived);
                req.SendRequest();
                 */
            }
            else
            {
                Debug.Print("Authentication failed because network is down");
            }

            authInProgress = false;
            timeOutTimer.Stop();
        }

        void req_ResponseReceived(HttpRequest sender, HttpResponse response)
        {
            if (response.StatusCode != "200")
            {
                Debug.Print(response.StatusCode);
                networkUp = false;
            }

            Debug.Print("Response received");
        }

        string getJsonString(string scannedRFID, GT.Picture capturedImage)
        {
            //string jsonString = "";
            CRequestData testObj = new CRequestData();
            testObj.RFID = scannedRFID;
            testObj.image = capturedImage;
            string json = JsonSerializer.SerializeObject(testObj);
            Debug.Print(json);
            return json;
        }

        private void rfidReader_IdReceived(RFIDReader sender, string e)
        {
            if (authInProgress == false)
            {
                Debug.Print("RFID scanned: " + e);
                scannedRFID = e;
                if (camera.CameraReady)
                {
                    authInProgress = true;
                    camera.TakePicture();
                    timeOutTimer.Start();
                    timeOutTimer.Tick += timeOutTimer_Tick;
                }
                else
                {
                    Debug.Print("Camera not ready");
                }
            }
            else
            {
                Debug.Print("Authentication already in progress");
            }
            /*displayTE35.SimpleGraphics.Clear();
            displayTE35.SimpleGraphics.DisplayText("RFID scanned: " + e, fontNina, GT.Color.White, 10, 10);*/
        }

        void timeOutTimer_Tick(GT.Timer timer)
        {
            if (authInProgress)
            {
                authInProgress = false;
                Debug.Print("Authentication failed because of time-out");
            }
            timeOutTimer.Stop();
        }

        private void rfidReader_MalformedIdReceived(RFIDReader sender, EventArgs e)
        {
            Debug.Print("Please rescan your card");
            displayTE35.SimpleGraphics.Clear();
            displayTE35.SimpleGraphics.DisplayText("Please rescan your card", fontNina, GT.Color.White, 10, 10);
        }
    }
}
