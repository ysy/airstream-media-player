/*
   Copyright (C) 2011 Tom Thorpe

   This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation; either version 2 of the License, or (at your option) any later version.

   This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details. You should have received a copy of the GNU General Public License along with this program; if not, write to the Free Software Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
*/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;

namespace AirStreamPlayer
{
    /// <remarks>
    /// Contains all the delegates and callbacks for the threadsafe accessors of GUI elements in the Publish class
    /// </remarks>
    public partial class Publish : Form
    {
        /// <summary>
        /// Delegate for appendToMessageBox to use if it's not in the right thread
        /// </summary>
        /// <param name="text">The text to append to the message box</param>
        delegate void appendToMessagesBoxDelegate(string text);
        /// <summary>
        /// Appends data to the messagesBox textbox control in a thread-safe way (by making sure the messagesBox object is accessed from the same thread it was created on)
        /// </summary>
        /// <param name="text">The text to append to the message box</param>
        private void appendToMessagesBox(string text)
        {
            if (messagesBox.InvokeRequired) //we're in the wrong thread. Appending the string from here wouldn't be thread-safe. Need to make a delegate to do the appending, and queue that to be called from the right thread.
            {
                //make the delegate
                appendToMessagesBoxDelegate d = new appendToMessagesBoxDelegate(appendToMessagesBox); //will call this method again, but this time from the right thread
                messagesBox.Invoke(d, new object[] { text }); //will call the delegate above with the text value. This mesages appendToMessagesBox(text) gets queued to run in the same thread as messagesBox was created in. This makes it thread safe, as two threads cant try to access messagesBox at the same time.
            }
            else //we're in the right thread, just append the string.
            {
                messagesBox.Text += text;

                //scroll to the bottom of the text box (this will also move the caret position, if you wanted to scroll without doing that youd have to import a dll and do some stuff, not really that fussed)
                messagesBox.SelectionStart = messagesBox.TextLength;
                messagesBox.ScrollToCaret();
            }
        }



        //This delegate and method provides a thread-safe way to pass a playback message to the player
        /// <summary>
        /// Delegate used by sendPlaybackEvent() event to send playback messages to the player object in a thread-safe way
        /// </summary>
        /// <param name="action">The action to perform</param>
        /// <param name="param">Any extra data that might go with that action (eg the seek position). For playURL two pieces of information can be given, the URL then the start position which is a number between 0 and 1, where 0.5 is halfway through the file (in that order)</param>
        delegate void sendPlaybackEventCallback(string action, params string[] parameters);
        /// <summary>
        /// Can be called to provide a thread safe way to pass a playback message to the player object.
        /// The events to be performed (eg play, pause, seek) will often be sent to the Server from the iOS device, meaning they will be received on one of the Server's threads. If the server directly accessed the player component, this would not be thread safe and could cause crashes.
        /// This method will check if the request to access player is being called on the right thread, if so it will just perform the requested action, if not it will request that the method be reinvoked (using the sendPlaybackEventCallback) on the correct thread (the same thread that created player, ensuring nobody tries to access player when it isn't available).
        /// </summary>
        /// <param name="action">The action to perform (eg "play", "pause", "stop", "scrub"</param>
        /// <param name="param">Any extra data that might go with that action (eg the seek position). For playURL two pieces of information can be given, the URL then the start position which is a number between 0 and 1, where 0.5 is halfway through the file (in that order)<</param>
        private void sendPlaybackEvent(string action, params string[] parameters)
        {
            //quicktime player
            if (usingQuicktime)
            {
                if (quicktimePlayer.InvokeRequired)//wrong thread, send callback
                {
                    sendPlaybackEventCallback d = new sendPlaybackEventCallback(sendPlaybackEvent);

                    //first prepare the parameters that need to get passed to the callback
                    object[] callbackParams = new object[] { action, parameters };

                    //now invoke the callback with the parameters
                    quicktimePlayer.Invoke(d, callbackParams);
                }
                else //right thread :-)
                {
                    if (action.Equals("pause"))
                    {
                        Debug.WriteLine("pausing player");
                        if (quicktimePlayer.Movie != null)
                        {
                            quicktimePlayer.Movie.Pause();
                        }
                    }
                    else if (action.Equals("play"))
                    {
                        Debug.WriteLine("playing player");
                        if (quicktimePlayer.Movie != null)
                        {
                            quicktimePlayer.Movie.Play();
                        }
                    }
                    else if (action.Equals("stop"))
                    {
                        Debug.WriteLine("stopping player");
                        if (quicktimePlayer.Movie != null)
                        {
                            removeMovieEventListeners(quicktimePlayer.Movie);
                            quicktimePlayer.Movie.Stop();
                        }
                    }
                    else if (action.Equals("scrub"))
                    {
                        if (parameters.Length > 0) //if the scrub position exists in the paramters (should be first arg)...
                        {
                            Debug.WriteLine("scrubbing to position: {0}", parameters[0]);
                            if (quicktimePlayer.Movie != null)
                            {
                                int timescale = quicktimePlayer.Movie.TimeScale; //get the timescale of the file, this will be used to multiply the input time (in seconds) by to get the quicktime time. (quicktime uses these durations instead of seconds like WMP).
                                if (timescale == 0)
                                {
                                    timescale = 1;
                                }
                                quicktimePlayer.Movie.Time = (int)Convert.ToDouble(parameters[0]) * timescale; //could have tried toInt32, but that causes exception when string is a double "3.2453". Also, quicktime seek time is seconds*timescale
                            }
                        }
                    }
                    else if (action.Equals("playUrl"))
                    {
                        if (parameters.Length > 0) //if the playback url exists in the paramters (should be first arg)...
                        {
                            imageToolStripMenuItem.Enabled = false;

                            //TODO: At the moment, m4v files direct from the iDevice aren't supported as I dont know how to pass the PIC request on to quicktime, maybe fix it in future. For now, I have to ignore it.
                            if (parameters[0].Trim().EndsWith("m4v"))
                            {
                                return;
                            }

                            //if the application is minimised to system tray and it isn't set to go fullscreen on playing a file, restore it ready to play the file. If the start video fullscreen option has been chosen, there's no point restoring the window (and people might actually want it left in the system tray, to mean it can be left running all the time without interfering)
                            if (systemTrayIcon.Visible == true && !startVideosFullscreenToolStripMenuItem.Checked)
                            {
                                systemTrayIcon_DoubleClick(this, null);
                            }

                            //remove old listeners
                            removeMovieEventListeners(quicktimePlayer.Movie);
                            
                            //make the right components visible
                            setPictureBoxVisibility(false);
                            setQuicktimePlayerVisibility(true);
                            
                            
                            //set the url and send the loading status message
                            Debug.WriteLine("Quicktime player attempting to play URL" + parameters[0]);
                            quicktimePlayer.URL = parameters[0];
                            theServer.sendStatusMessage("loading");

                            //if a start position was given, seek to it
                            if (parameters.Length > 1 && quicktimePlayer.Movie != null)
                            {
                                double startPosition = Convert.ToDouble(parameters[1]);
                                if (startPosition > 0) //don't bother doing anything if it's just 0
                                {
                                    //the position of the track is the startPosition * total length of track
                                    quicktimePlayer.Movie.Time = (int)(startPosition * quicktimePlayer.Movie.EndTime);
                                }
                            }


                            if (quicktimePlayer.Movie != null)
                            {
                                videoHasAlreadyBeenStarted = false; //new video started, so set this to false.
                                addMovieEventListeners(quicktimePlayer.Movie);
                                quicktimePlayer.Movie.Play();                     
                            }
                        }
                    }
                }
            }
            else
            {
                if (player.InvokeRequired) //wrong thread, send callback
                {
                    sendPlaybackEventCallback d = new sendPlaybackEventCallback(sendPlaybackEvent);

                    //first prepare the parameters that need to get passed to the callback
                    object[] callbackParams = new object[] { action, parameters };

                    //now invoke the callback with the parameters
                    player.Invoke(d, callbackParams);
                }
                else //right thread :-)
                {
                    if (action.Equals("pause"))
                    {
                        Debug.WriteLine("pausing player");
                        player.Ctlcontrols.pause();
                    }
                    else if (action.Equals("play"))
                    {
                        Debug.WriteLine("playing player");
                        player.Ctlcontrols.play();
                    }
                    else if (action.Equals("stop"))
                    {
                        Debug.WriteLine("stopping player");
                        player.Ctlcontrols.stop();
                    }
                    else if (action.Equals("scrub"))
                    {
                        if (parameters.Length > 0)//if the scrub position exists in the paramters (should be first arg)...
                        {
                            player.Ctlcontrols.currentPosition = Convert.ToDouble(parameters[0]);
                        }
                    }
                    else if (action.Equals("playUrl"))
                    {
                        if (parameters.Length > 0)//if the playback url exists in the paramters (should be first arg)...
                        {
                            imageToolStripMenuItem.Enabled = false;

                            //TODO: At the moment, m4v files direct from the iDevice aren't supported as I dont know how to pass the PIC request on to quicktime, maybe fix it in future. For now, I have to ignore it.
                            if (parameters[0].Trim().EndsWith("m4v"))
                            {
                                return;
                            }

                            //if the application is minimised to system tray and it isn't set to go fullscreen on playing a file, restore it ready to play the file. If the start video fullscreen option has been chosen, there's no point restoring the window (and people might actually want it left in the system tray, to mean it can be left running all the time without interfering)
                            if (systemTrayIcon.Visible == true && !startVideosFullscreenToolStripMenuItem.Checked)
                            {
                                systemTrayIcon_DoubleClick(this, null);
                            }

                            //make the right component visible
                            setPictureBoxVisibility(false);
                            setPlayerVisibility(true);

                            //set the url and send the loading status message
                            player.URL = parameters[0];
                            theServer.sendStatusMessage("loading");

                            videoHasAlreadyBeenStarted = false; //new video started loading, so set this to false.

                            //if a start position was given, seek to it
                            if (parameters.Length > 1)
                            {
                                double startPosition = Convert.ToDouble(parameters[1]);
                                if (startPosition > 0) //don't bother doing anything if it's just 0
                                {
                                    player.Ctlcontrols.currentPosition = player.currentMedia.duration * startPosition; //FIXME: fix this, as WMP always returns 0 as the duration when it's loading, so skipping to the correct position doesn't work.
                                }
                            }
                        }
                    }
                }
            }
        }



        /// <summary>
        /// Callback Used by getPlayerPosition() to re-call the method on the right thread if currently in the wrong thread
        /// </summary>
        /// <returns>The player's current position in seconds</returns>
        delegate int getPlayerPositionCallback();
        /// <summary>
        /// Gets the player's current position in a thread-safe way (so it can be accessed from any thread, including threads made by the Server instance)
        /// </summary>
        /// <returns>The player's current position in seconds</returns>
        public int getPlayerPosition()
        {
            if (usingQuicktime) //quicktime
            {
                if (quicktimePlayer.InvokeRequired) //wrong thread!
                {
                    return (int)quicktimePlayer.Invoke(new getPlayerPositionCallback(getPlayerPosition)); //recall the method in the correct thread
                }
                else //right thread! 
                {
                    if (quicktimePlayer.Movie != null)
                    {
                        int timescale = quicktimePlayer.Movie.TimeScale; //get the timescale of the file, this will be used to divide the quicktime position by to get the position in seconds. (quicktime uses these durations instead of seconds like WMP).
                        if (timescale == 0)
                        {
                            timescale = 1;
                        }
                        return (int)quicktimePlayer.Movie.Time / timescale; //quicktime displays time in 1/1000 second, iOS uses seconds.
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            else //wmp
            {
                if (player.InvokeRequired) //wrong thread!
                {
                    return (int)player.Invoke(new getPlayerPositionCallback(getPlayerPosition)); //recall the method in the correct thread
                }
                else //right thread! 
                {
                    return (int)player.Ctlcontrols.currentPosition;
                }
            }
        }

        /// <summary>
        /// Callback Used by getPlayerDuration() to re-call the method on the right thread if currently in the wrong thread
        /// </summary>
        /// <returns></returns>
        delegate int getPlayerDurationCallback();
        /// <summary>
        /// Gets the player's current media duration in a thread-safe way (so it can be accessed from any thread, including threads made by the Server instance)
        /// </summary>
        /// <returns>The duration of the media being played by the player in seconds</returns>
        public int getPlayerDuration()
        {
            if (usingQuicktime) //quicktime
            {
                if (quicktimePlayer.InvokeRequired) //wrong thread!
                {
                    return (int)quicktimePlayer.Invoke(new getPlayerPositionCallback(getPlayerDuration)); //recall the method in the correct thread
                }
                else //right thread! 
                {
                    if (quicktimePlayer.Movie != null)
                    {
                        int timescale = quicktimePlayer.Movie.TimeScale; //get the timescale of the file, this will be used to divide the duration by and get the time in seconds. (quicktime uses these durations instead of seconds like WMP).
                        if (timescale == 0)
                        {
                            timescale = 1;
                        }
                        int duration = (int)quicktimePlayer.Movie.Duration / timescale;
                        return duration;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            else //wmp
            {
                if (player.InvokeRequired) //wrong thread!
                {
                    return (int)player.Invoke(new getPlayerDurationCallback(getPlayerDuration)); //recall in the correct thread
                }
                else //right thread!
                {
                    if (player.currentMedia != null) //current media might not be loaded yet. If it's loaded, get the duration, if it's not return 0.
                    {
                        return (int)player.currentMedia.duration;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
        }



        /// <summary>
        /// Delegate for callback of setPlayerVisibility.
        /// </summary>
        /// <param name="visible">Whether the player is visible</param>
        public delegate void playerVisibilityCallback(bool visible);
        /// <summary>
        /// Sets the WMP player's visibility in a thread-safe way.
        /// </summary>
        /// <param name="visible">Whether the player is visible</param>
        public void setPlayerVisibility(bool visible)
        {
            if (player.InvokeRequired) //wrong thread
            {
                playerVisibilityCallback d = new playerVisibilityCallback(setPlayerVisibility);
                player.Invoke(d, new object[] { visible });
            }
            else //right thread
            {
                player.Visible = visible;
            }
        }


        /// <summary>
        /// Delegate for callback of setQuicktimePlayerVisibility.
        /// </summary>
        /// <param name="visible">Whether the player is visible</param>
        public delegate void quicktimePlayerVisibilityCallback(bool visible);
        /// <summary>
        /// Sets the quicktime player's visibility in a thread-safe way.
        /// </summary>
        /// <param name="visible">Whether the player is visible</param>
        public void setQuicktimePlayerVisibility(bool visible)
        {
            if (quicktimePlayer.InvokeRequired) //wrong thread
            {
                quicktimePlayerVisibilityCallback d = new quicktimePlayerVisibilityCallback(setQuicktimePlayerVisibility);
                quicktimePlayer.Invoke(d, new object[] { visible });
            }
            else //right thread
            {
                quicktimePlayer.Visible = visible;
            }
        }


        /// <summary>
        /// Delegate used by setVideoFullscreen to control whether the player is fullscreen or not.
        /// </summary>
        /// <param name="fullscreen">Whether the player should be fullscreen</param>
        private delegate void setVideoFullscreenCallback(bool fullscreen);
        /// <summary>
        /// Sets the fullscreen state of the current player (wmp/quicktime) in a threadsafe way, meaning you can call it from the GUI but also from one of the server threads even though the gui elements themselves may not be threadsafe
        /// Even if you know you're already likely to be in the right thread (eg youre handling a GUI event), you should still use this method as it will check which player to set the fullscreen property of for you.
        /// </summary>
        /// <param name="fullscreen">Whether the player should be fullscreen</param>
        private void setVideoFullscreen(bool fullscreen)
        {
            //quicktime player
            if (usingQuicktime)
            {
                if (quicktimePlayer.InvokeRequired)//wrong thread, send callback
                {
                    setVideoFullscreenCallback d = new setVideoFullscreenCallback(setVideoFullscreen);
                    quicktimePlayer.Invoke(d, new object[] { fullscreen });
                }
                else //right thread :-)
                {
                    if (fullscreen) //we have to do some custom things for quicktime fullscreen to make it work properly, and also allow you to exit fullscreen (the esc key property doesnt always seem to work)
                    {
                        this.quicktimePlayer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left))); //either the quicktime player or c# has a problem with anchoring in fullscreen, which means the video doesnt get stretched to the full size of the screen if anchored. So remove the custom anchors, and set them back when fullscreen is finished (rememeber to do this in the QTEvent for ending fullscreen)
                        quicktimePlayer.FullScreen = fullscreen;
                        this.KeyDown += Publish_KeyDown; //add the listener to key events to end the fullscreen
                        quicktimePlayer.Focus();
                    }
                    else
                    {
                        if (quicktimePlayer.Movie != null)
                        {
                            quicktimePlayer.FullScreen = fullscreen;
                            this.KeyDown -= Publish_KeyDown; //remove the key listener as we don't need it to exit fullscreen anymore and we dont want events triggered trying to exit fullscreen on every keypress when it's not fullscreen
                        }
                        else
                        {
                            if (fullscreen) //if trying to fullscreen when no movie is loaded...
                            {
                                MessageBox.Show("Start playing a movie before choosing fullscreen", "Start Movie");
                            }
                        }
                    }
                }
            }
            else //wmp player
            {
                if (player.InvokeRequired) //wrong thread, send callback
                {
                    setVideoFullscreenCallback d = new setVideoFullscreenCallback(setVideoFullscreen);
                    player.Invoke(d, new object[] { fullscreen });
                }
                else //right thread :-)
                {
                    if (fullscreen == true && (player.playState == WMPLib.WMPPlayState.wmppsStopped || player.URL == ""))
                    {
                        MessageBox.Show("Start playing a movie before choosing fullscreen", "Start Movie");
                    }
                    else
                    {
                        player.fullScreen = fullscreen;
                    }
                }
            }
        }



        /// <summary>
        /// Delegate for callback of setPictureBoxVisibility
        /// </summary>
        /// <param name="visible">Whether the picture box is visible</param>
        public delegate void pictureBoxVisibilityCallback(bool visible);
        /// <summary>
        /// Sets the picture box's visibility in a thread-safe way.
        /// </summary>
        /// <param name="visible">Whether the picture box is visible</param>
        public void setPictureBoxVisibility(bool visible)
        {
            if (pictureBox.InvokeRequired) //wrong thread
            {
                pictureBoxVisibilityCallback d = new pictureBoxVisibilityCallback(setPictureBoxVisibility);
                pictureBox.Invoke(d, new object[] { visible });
            }
            else //right thread
            {
                pictureBox.Visible = visible;
            }
        }

        /// <summary>
        /// Delegate for callback of setPictureBoxPicture
        /// </summary>
        /// <param name="visible">Sets the picture box's image</param>
        public delegate void setPictureBoxPictureCallback(Image theImage);
        /// <summary>
        /// Sets the picture box's Image in a thread-safe way. Makes the picture box visible and hides the video box.
        /// </summary>
        /// <param name="visible">Whether the picture box is visible</param>
        public void setPictureBoxPicture(Image theImage)
        {
            if (pictureBox.InvokeRequired) //wrong thread
            {
                setPictureBoxPictureCallback d = new setPictureBoxPictureCallback(setPictureBoxPicture);
                pictureBox.Invoke(d, new object[] { theImage });
            }
            else //right thread
            {
                //stop any movie playing if there is one
                sendPlaybackEvent("stop");

                //disable fullscreen if the player was fullscreen
                setVideoFullscreen(false);

                //restore the application if it was minimised to the system tray
                if (systemTrayIcon.Visible)
                {
                    systemTrayIcon_DoubleClick(this, null);
                }

                //hide the video player
                if (usingQuicktime)
                {
                    setQuicktimePlayerVisibility(false);
                }
                else
                {
                    setPlayerVisibility(false);
                }

                //enable the image toolbar item
                imageToolStripMenuItem.Enabled = true;

                //show the picture box and set its image
                setPictureBoxVisibility(true);
                pictureBox.Image = theImage;
            }
        }



        /// <summary>
        /// Delegate for ShowMessageBox()
        /// </summary>
        /// <param name="s1"></param>
        /// <param name="s2"></param>
        /// <returns></returns>
        public delegate DialogResult PassStringStringReturnDialogResultDelegate(String s1, String s2);
        /// <summary>
        /// Shows a message box in the correct thread for the GUI (so it can be called from the server)
        /// </summary>
        /// <param name="message">The message to go in the message box</param>
        /// <param name="caption">The title of the message box</param>
        /// <returns></returns>
        public DialogResult ShowMessageBox(String message, String caption)
        {
            if (this.InvokeRequired)
            {
                return (DialogResult)this.Invoke(new PassStringStringReturnDialogResultDelegate(ShowMessageBox), message, caption);
            }
            return MessageBox.Show(this, message, caption);
        }

    }
}
