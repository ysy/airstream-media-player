/*
   Copyright (C) 2011 Tom Thorpe

   This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation; either version 2 of the License, or (at your option) any later version.

   This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details. You should have received a copy of the GNU General Public License along with this program; if not, write to the Free Software Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ZeroconfService;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using QTOLibrary;
using QTOControlLib;

namespace AirStreamPlayer
{
    public partial class Publish : Form
    {
        private const string domain = "local";
        private const string type = "_airplay._tcp";
        private string name = String.Format("{0} - {1}", SystemInformation.ComputerName, "Air Stream Player");
        private const int port = 7000;

        bool publishing = false;
        NetService publishService = null;
        Server theServer = null;
        bool quicktimeAvailable = true; //optimistic
        bool usingQuicktime = true; 

        /// <remarks>
        /// The controller part of the app, goes with the partial Publisher.Designer class which defines the GUI
        /// </remarks>
        public Publish()
        {
            try
            {
                InitializeComponent();
            }
            catch (COMException)
            {
                //quicktime is not installed
                //if the user has not chosen to hide the warning message, show the message. Otherwise, if the user has previously clicked "Don't show this message again", don't show the message
                if (!Properties.Settings.Default.dismissQuicktimeWarning)
                {
                    using (CheckboxMessageBox box = new CheckboxMessageBox("Quicktime is not installed, using Windows Media Player instead. If you do not have the right WMP codecs installed, this might not work"))
                    {
                        bool dontshowagain = box.showMessage(); //show the message box, and get the result of whether the dont show this again checkbox was checked
                        if (dontshowagain) //if the user chose not to show the message again
                        {
                            Properties.Settings.Default.dismissQuicktimeWarning = true; //save the result of the user choosing to not show the message again.
                            Properties.Settings.Default.Save();
                        }
                    }
                }
                quicktimeAvailable = false; //quicktime isn't available
                
                //WMP is the only option available, so disable the option to "prefer" it from the file menu
                useWindowsMediaPlayerToolStripMenuItem.Enabled = false;
            }

            //check if Bonjour is installed and exit app if it isn't
            if (checkBonjourInstalled())
            {            
                //display the right player (quicktime if it's available, WMP otherwise)
                if (quicktimeAvailable && !Properties.Settings.Default.useWMPInstead) //if quicktime is available, and the user hasn't chosen to use WMP instead, show quicktime
                {
                    useWindowsMediaPlayerToolStripMenuItem.Checked = false;
                    usingQuicktime = true;
                    quicktimePlayer.Show();
                    player.Dispose();
                    player = null;
                }
                else //otherwise, if quicktime isn't available, or the user has specifically chosen to use WMP instead, show WMP
                {
                    useWindowsMediaPlayerToolStripMenuItem.Checked = true;
                    usingQuicktime = false;
                    player.Show();
                    quicktimePlayer.Dispose();
                    quicktimePlayer = null;
                }

                //start the server to receive incoming connections from iOS devices
                theServer = new Server(port);
                theServer.Start();

                //add the delegate to do something when a client connects to the server
                theServer.clientConnected += new Server.clientConnectedHandler(theServer_clientConnected);

                //add the delegate to do something when the server sends a message to the client
                theServer.messageSent += new Server.messageSentHandler(theServer_messageSent);

                //add the delegate to do something when the client sends a play url request
                theServer.playURL += new Server.urlPlayMessageHandler(theServer_playURL);

                //add the delegate to do something when a playback event is received
                theServer.playbackEvent += new Server.playbackMessageHandler(theServer_playbackEvent);

                //add the delegate to do something when play image event is received
                theServer.playImage += new Server.imageMessageHandler(theServer_playImage);

                //add the delegate to do something when authorisation key is received from server
                theServer.authorisationRequest += new Server.authorisationRequestHandler(theServer_authorisationRequest);

                //start publishing the airplay service over Bonjour.
                DoPublish();

                //check if the debug window should be displayed, and display it if so
                if (Properties.Settings.Default.debug == true)
                {
                    setDebugVisibility(true);
                }
            }
            else
            {
                //exit the application because bonjour isn't installed.
                if (System.Windows.Forms.Application.MessageLoop)
                {
                    // WinForms app
                    System.Windows.Forms.Application.Exit();
                }
                else
                {
                    // console app
                    System.Environment.Exit(1);
                }
            }
        }

        private bool checkBonjourInstalled()
        {
            Version bonjourVersion = null;

            //check if Bonjour is installed by attempting to print it's version
            try
            {
                bonjourVersion = NetService.DaemonVersion;
                Debug.WriteLine(String.Format("Bonjour Version: {0}", NetService.DaemonVersion));
            }
            catch (Exception ex)
            {
                String message = ex is DNSServiceException ? "Could not find Bonjour. Do you have it installed?\nIf not, please download and install it from the Apple website.\nhttp://support.apple.com/kb/DL999" : ex.Message; //if you got an exception when you tried to print the version, Bonjour is not installed. Or you might get some other exception here, so show that too
                MessageBox.Show(message, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            //it seems sometimes the version still returns even when bonjour isn't installed, but returns version 0, so check that too.
            if (bonjourVersion == null || bonjourVersion.MajorRevision == 0)
            {
                MessageBox.Show("Could not find Bonjour. Do you have it installed?\nIf not, please download and install it from the Apple website.\nhttp://support.apple.com/kb/DL999", "Bonjour Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }


        /// <summary>
        /// Starts publishing the airplay service over Bonjour so that iOS devices can find it.
        /// This only advertises the service, Bonjour doesn't deal with the connections themselves. The connections are dealt with in the Server class.
        /// </summary>
        private void DoPublish()
        {
            publishService = new NetService(domain, type, name, port);

            //add delegates for success/false
            publishService.DidPublishService += publishService_DidPublishService;
            publishService.DidNotPublishService += publishService_DidNotPublishService;

            //add txtrecord, which gives details of the service. For now we'll just put the version number
            System.Collections.Hashtable dict = new System.Collections.Hashtable();
            dict.Add("txtvers", "1");
            publishService.TXTRecordData = NetService.DataFromTXTRecordDictionary(dict);

            publishService.Publish();

        }

        /// <summary>
        /// Stops publishing the airplay service over Bonjour.
        /// </summary>
        private void StopPublish()
        {
            //if the publish service is set up, stop it.
            if (publishService != null)
            {
                publishService.Stop();
                Debug.WriteLine("Stopped publishing");
            }

            publishing = false;
        }

        /// <summary>
        /// Sets whether the debug box is visible or not
        /// </summary>
        /// <param name="visible">The debug box visibility</param>
        private void setDebugVisibility(bool visible)
        {
            int padding = 5; //the space between the debug box and the player
            //we need to make the player component smaller or bigger to accomodate the debug box.
            //work out where the top of the player component should be by getting the position of the debug box's top and adding it to the debug box's height, plus some padding.
            int debugheight = DebugBox.Height; //working out these heights here means resizing the debug box is still possible using the designer if we ever want to without having to change code
            int debugtop = DebugBox.Location.Y;
            int debugSpace = debugheight + debugtop + padding;

            //minus the height of the top menustrip (0,0 starts above the menu strip)
            debugSpace -= menuStrip.Height;

            if (visible)
            {
                DebugBox.Visible = true;
                showDebugToolStripMenuItem.Checked = true;

                //we're showing the debug box, so the new player top will have to be (the old player top) + (the space that the debug box will take), moving it away from zero will move it away from the top of the screen, making room for the debug box.
                if (usingQuicktime)
                {
                    quicktimePlayer.Top = quicktimePlayer.Top + debugSpace;
                    quicktimePlayer.Height -= debugSpace;
                }
                else
                {
                    player.Top = player.Top + debugSpace;
                    player.Height -= debugSpace;
                }
                //do the same to the pictureBox
                pictureBox.Top = pictureBox.Top + debugSpace;
                pictureBox.Height -= debugSpace;
            }
            else
            {
                DebugBox.Visible = false;
                showDebugToolStripMenuItem.Checked = false;

                //we're hiding the debug box, so the new player top can be the old player top - the space that the debug box took up (the closer to 0 the top is, the closer to the top of the screen it is, 0,0 is the top left)
                if (usingQuicktime)
                {
                    quicktimePlayer.Top = quicktimePlayer.Top - debugSpace;
                    quicktimePlayer.Height += debugSpace;
                }
                else
                {
                    player.Top = player.Top - debugSpace;
                    player.Height += debugSpace;
                }
                //do the same to the pictureBox
                pictureBox.Top = pictureBox.Top - debugSpace;
                pictureBox.Height += debugSpace;
            }

        }

        /// <summary>
        /// Is called when the service is successfully published. 
        /// Conforms to the delegate specified by NetService's DidPublishService event.
        //  Writes a line to the Debug and changes the publishing variable value to true
        /// </summary>
        /// <param name="service"></param>
        private void publishService_DidPublishService(NetService service)
        {
            Debug.WriteLine("Published Bonjour Service: domain(" + service.Domain + ") type(" + service.Type + ") name(" + service.Name + ")");
            publishing = true;
        }

        /// <summary>
        /// Is called when the service attempted to be published but couldnt be for some reason.
        /// Conforms to the delegate specified by NetService's DidNotPublishService event.
        /// Displays error message and quits the application.
        /// </summary>
        /// <param name="service">The NetService that failed to successfully publish</param>
        /// <param name="exception">The exception that occured</param>
        private void publishService_DidNotPublishService(NetService service, DNSServiceException exception)
        {
            MessageBox.Show(String.Format("A DNSServiceException occured: {0}", exception.Message), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }

        /// <summary>
        /// Called when the Server instance sends a request for the application to play a URL.
        /// Conforms to the urlPlayMessageHandler delegate
        /// Sends the URL to the player object, and requests that the Server send the "loading" status message to the iOS device.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="url"></param>
        /// <param name="position">The position in the file to start from, a double between 0 and 1, where 0 is the beginning, 1 is the end, and 0.5 is halfway through the file. If a number not in this range is given, it will be ignored.</param>
        void theServer_playURL(object sender, string url, double position)
        {
            if (position < 0 || position > 1)
            {
                position = 0;
            }
            sendPlaybackEvent("playUrl", url, position.ToString());
        }

        /// <summary>
        /// Conforms to the delegate that the Server uses to sent the playImage event.
        /// Calls the setPictureBoxPicture() method to set the image in a thread-safe way. setPictureBoxPicture() also hides the video players and shows the picturebox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="theImage">The image to display</param>
        void theServer_playImage(object sender, Image theImage)
        {
            setPictureBoxPicture(theImage);
        }

        /// <summary>
        /// Is called when the Server instance receives a playback event from the iOS device (such as "play", "pause", "seek" etc).
        /// Conforms to the playbackMessageHandler delegate
        /// This could in theory be called from any random thread, as it will be triggered by the Server. So it does nothing but forward the data on to the sendPlaybackEvent() method, which will perform the action on the player object in a thread-safe way.
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="action">The action to perform</param>
        /// <param name="param">Any extra data that might go with that action (eg the seek position)</param>
        void theServer_playbackEvent(object sender, string action, string param)
        {
            sendPlaybackEvent(action, param); //pass it on to the thread-safe method.
        }

        /// <summary>
        /// Is called when the Server instance receives a message from a client.
        /// Conforms to the clientConnectedHandler delegate
        /// Constructs a message to write to the debug box saying the message was received and what data was in it, then calls appendToMessagesBox() to write this info to the debug box in a thread-safe way (as theServer_clientConnected() will be called from one of the Server threads, not the GUI thread)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        private void theServer_clientConnected(object sender, string message)
        {
            String text = "";
            text += "New message received.\r\n";
            text += "Data:\r\n";
            text += message;
            text += "\r\n---------------------------------------------------------------\r\n";
            appendToMessagesBox(text);
        }

        /// <summary>
        /// Is called when the Server instance sends a message to the client.
        /// Conforms to the MessageSentHandler delegate
        /// Constructs a message to write to the debug box saying the message was sent and what data was in it, then calls appendToMessagesBox() to write this info to the debug box in a thread-safe way (as theServer_messageSent() will be called from one of the Server threads, not the GUI thread)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        void theServer_messageSent(object sender, string message)
        {
            String text = "";
            text += "Message sent to client.\r\n";
            text += "Data:\r\n";
            text += message;
            text += "\r\n---------------------------------------------------------------\r\n";
            appendToMessagesBox(text);
        }

        /// <summary>
        /// Displays a message box saying that DRM videos are not currently supported
        /// </summary>
        void theServer_authorisationRequest()
        {
            ShowMessageBox("Sorry, DRM video playback from the iPod app is not currently supported", "DRM");
        }


        /// <summary>
        /// Called when the window is closing, which will be exiting the application.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Publish_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopPublish();
            if (theServer != null)
            {
                //stop the server when the window's closed
                theServer.Stop();
            }
        }


        /// <summary>
        /// If added as an event to the Form's keydown event, allows any key to exit quicktime's fullscreen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Publish_KeyDown(object sender, KeyEventArgs e)
        {
            if (usingQuicktime)
            {
                setVideoFullscreen(false);
            }
        }
 
        /// <summary>
        /// Specifies and adds the quicktime movie events (pause, play etc) to a quicktime movie. Anything that then listens to the QTEvent event will then pick up these events.
        /// </summary>
        /// <param name="myMovie">The movie to add the listeners to</param>
        private void addMovieEventListeners(QTMovie myMovie)
        {
            // Make sure movie is loaded
            if (myMovie == null) return;

            // rate change listener (pause, play)
            myMovie.EventListeners.Add(QTEventClassesEnum.qtEventClassStateChange, QTEventIDsEnum.qtEventRateWillChange, 0, null);

            myMovie.EventListeners.Add(QTEventClassesEnum.qtEventClassStateChange, QTEventIDsEnum.qtEventMovieDidEnd);

        }

        /// <summary>
        /// Removes all quicktime event listeners for the quicktime QTMovie movie
        /// </summary>
        /// <param name="myMovie">The movie to remove the listeners from</param>
        private void removeMovieEventListeners(QTMovie myMovie)
        {
            // Make sure movie is loaded
            if (myMovie != null)
            {
                // Remove all event listeners
                myMovie.EventListeners.RemoveAll();
            }
        }

        /// <summary>
        /// Handles the QTEvent events that are added to a movie by addMovieEventListeners
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void quicktimePlayer_QTEvent(object sender, AxQTOControlLib._IQTControlEvents_QTEventEvent e)
        {
            switch (e.eventID)
            {
                // rate change (pause.play)
                case (int)QTEventIDsEnum.qtEventRateWillChange:
                        var rate = (e.eventObject.GetParam(QTEventObjectParametersEnum.qtEventParamMovieRate));
                        int therate = Convert.ToInt16(rate);
                        if (therate > 0)
                        {
                            theServer.sendStatusMessage("playing");
                        }
                        else
                        {
                            theServer.sendStatusMessage("paused");
                        }
                        break;
   
                // end of video
                case (int)QTEventIDsEnum.qtEventMovieDidEnd:
                        theServer.sendStatusMessage("stopped");
                        break;

            }
        }


        /// <summary>
        /// Is called when the quicktime player has a status update such as moving to fullscreen or moving from fullscreen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void quicktimePlayer_StatusUpdate(object sender, AxQTOControlLib._IQTControlEvents_StatusUpdateEvent e)
        {
            switch (e.statusCodeType)
            {
                case (int)QTStatusCodeTypesEnum.qtStatusCodeTypeControl:
                    {
                        switch (e.statusCode)
                        {
                            // fullscreen begin
                            case (int)QTStatusCodesEnum.qtStatusFullScreenBegin:
                                this.Hide();	// hide movie window
                                break;

                            // fullscreen end
                            case (int)QTStatusCodesEnum.qtStatusFullScreenEnd:
                                //restore the anchoring back (either quicktime or c# had a problem with anchors)
                                this.quicktimePlayer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
                                quicktimePlayer.SetScale(1);	// set back to a reasonable size
                                this.Show();	// show movie window again
                                break;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Is called whenever the status of the WMP player is changed (eg starts playing, is paused, stops)
        /// Sends these messages on to the iOS device using the Server's sendStatusMessage() method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">The playback event</param>
        private void player_PlayStateChange(object sender, AxWMPLib._WMPOCXEvents_PlayStateChangeEvent e)
        {
            //media player control's playstate change event handler
            if (player.playState == WMPLib.WMPPlayState.wmppsPlaying)
            {
                Debug.WriteLine("Playerstate changed to started");
                theServer.sendStatusMessage("playing");

            }
            else if (player.playState == WMPLib.WMPPlayState.wmppsPaused)
            {
                Debug.WriteLine("Playerstate changed to paused");
                theServer.sendStatusMessage("paused");
            }
            else if (player.playState == WMPLib.WMPPlayState.wmppsStopped)
            {
            }

        }

        /// <summary>
        /// Displays an about box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About aboutBox = new About();
            aboutBox.ShowDialog();
        }

        /// <summary>
        /// Exits the application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (theServer != null)
            {
                StopPublish();
                theServer.Stop();
            }
            Application.Exit();
        }

        /// <summary>
        /// Makes the current player fullscreen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fullscreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //just pass the info on to the method for making the player fullscreen, nothing more to do here!
            setVideoFullscreen(true);
        }

        /// <summary>
        /// Either hides or shows (toggles) the debug messages at the top of the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void showDebugToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DebugBox.Visible == true) //its currently visible, so make it hidden.
            {
                Properties.Settings.Default.debug = false;
                setDebugVisibility(false);
            }
            else //its currently hidden, so make it visible.
            {
                Properties.Settings.Default.debug = true;
                setDebugVisibility(true);
            }
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Toggles whether to prefer the use of Windows Media Player or Quicktime as the player
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void useWindowsMediaPlayerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //check if the option is currently checked. If it's checked, uncheck it and save the option. Otherwise, do the oposite.
            useWindowsMediaPlayerToolStripMenuItem.Checked = (!useWindowsMediaPlayerToolStripMenuItem.Checked);
            Properties.Settings.Default.useWMPInstead = useWindowsMediaPlayerToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
            
            //rather than try and reinitialise the alternative player from scratch, just ask the user to restart the program for now. This'll then load and show the correct player.
            String wmpMessage = "";
            if (useWindowsMediaPlayerToolStripMenuItem.Checked)
            {
                wmpMessage = "\nNote: Windows Media Player will probably only work on Windows 7 upwards, or if you have the right codecs installed";
            }
            MessageBox.Show("In order to make your changes take effect, please restart the program"+wmpMessage, "Restart Program to change", MessageBoxButtons.OK, MessageBoxIcon.Information );
        }

        private void saveImageAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox.Image != null)
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "jpeg Image|*.jpg|Bitmap Image|*.bmp|Gif Image|*.gif|PNG Image|*.png";
                    dialog.Title = "Save an Image File";

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        System.Drawing.Imaging.ImageFormat theFormat;
                        switch (dialog.FilterIndex)
                        {
                            case 1: //FilterIndex seems to start at 1, not zero
                                theFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                                break;
                            case 2:
                                theFormat = System.Drawing.Imaging.ImageFormat.Bmp;
                                break;
                            case 3:
                                theFormat = System.Drawing.Imaging.ImageFormat.Gif;
                                break;
                            case 4:
                                theFormat = System.Drawing.Imaging.ImageFormat.Png;
                                break;
                            default: //should never get reached, but just in case something weird happens
                                theFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                                break;      
                        }
                        pictureBox.Image.Save(dialog.FileName, theFormat);
                    }
                }
            }

        }


    }
}