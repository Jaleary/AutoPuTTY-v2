﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;
using System.Windows.Forms;
using AutoPuTTY.Properties;
using AutoPuTTY.Resources;
using AutoPuTTY.Utils.Datas;

namespace AutoPuTTY.Utils
{
    class connectionHelper
    {
        private static string[] f = { "\\", "/", ":", "*", "?", "\"", "<", ">", "|" };
        private static string[] ps = { "/", "\\\\" };
        private static string[] pr = { "\\", "\\" };

        private static string _currentGroup;
        private static string _currentServer;

        private static string _rdpOutPath;

        public static void StartConnect(string type, TreeNode selectedNode)
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException();

            if (selectedNode?.Parent == null) return;

            _currentGroup = selectedNode.Parent.Text;
            _currentServer = selectedNode.Text;

            ServerElement serverElement = xmlHelper.getServerByName(_currentGroup, _currentServer);
            if (serverElement == null) return;

            switch (serverElement.Type)
            {
                case "1": //RDP
                    LaunchRdp(serverElement);
                    break;
                case "2": //VNC
                    LaunchVnc(serverElement);
                    break;
                case "3": //WinSCP (SCP)
                    LaunchWinScp("scp://", serverElement);
                    break;
                case "4": //WinSCP (SFTP)
                    LaunchWinScp("sftp://", serverElement);
                    break;
                case "5": //WinSCP (FTP)
                    LaunchWinScp("ftp://", serverElement);
                    break;
                default: //PuTTY
                    LaunchPuTTy(serverElement);
                    break;
            }
        }

        private static void LaunchRdp(ServerElement serverElement)
        {
            string[] rdpExtractFilePath = ExtractFilePath(Settings.Default.rdpath);
            string rdpPath = Environment.ExpandEnvironmentVariables(rdpExtractFilePath[0]);
            string rdpLaunchArgs = rdpExtractFilePath[1];

            if (File.Exists(rdpPath))
            {
                Mstscpw defaultRdpLauncher = new Mstscpw();

                string[] sizes = Settings.Default.rdsize.Split('x');

                _rdpOutPath = "";

                if (Settings.Default.rdfilespath != "" && otherHelper.ReplaceA(ps, pr, Settings.Default.rdfilespath) != "\\")
                {
                    _rdpOutPath = otherHelper.ReplaceA(ps, pr, Settings.Default.rdfilespath + "\\");

                    //TODO: add try for exception
                    if (!Directory.Exists(_rdpOutPath))
                        Directory.CreateDirectory(_rdpOutPath);
                }

                
                TextWriter rdpFileWriter = new StreamWriter(path: _rdpOutPath + otherHelper.ReplaceU(f, serverElement.Name) + ".rdp");

                rdpFileWriter.WriteLine(Settings.Default.rdsize == "Full screen" ? "screen mode id:i:2" : "screen mode id:i:1");
                rdpFileWriter.WriteLine(sizes.Length == 2 ? "desktopwidth:i:" + sizes[0] : "");
                rdpFileWriter.WriteLine(sizes.Length == 2 ? "desktopheight:i:" + sizes[1] : "");
                rdpFileWriter.WriteLine(serverElement.HostWithServer != "" ? "full address:s:" + serverElement.HostWithServer : "");
                rdpFileWriter.WriteLine(serverElement.Username != "" ? "username:s:" + serverElement.Username : "");
                rdpFileWriter.WriteLine(serverElement.Username != "" && serverElement.Password != "" ? "password 51:b:" + defaultRdpLauncher.encryptpw(serverElement.Password) : "");
                rdpFileWriter.WriteLine(Settings.Default.rddrives ? "redirectdrives:i:1" : "");
                rdpFileWriter.WriteLine(Settings.Default.rdadmin ? "administrative session:i:1" : "");
                rdpFileWriter.WriteLine(Settings.Default.rdspan ? "use multimon:i:1" : "");

                rdpFileWriter.Close();

                Process myProc = new Process
                {
                    StartInfo =
                    {
                        FileName = rdpPath,
                        Arguments = "\"" + _rdpOutPath + otherHelper.ReplaceU(f, serverElement.Name) + ".rdp\"" + (rdpLaunchArgs != null ? " " + rdpLaunchArgs : ""),
                    }
                };

                myProc.Start();
            }
            else
            {
                if (MessageBox.Show("Could not find file \"" + rdpPath + "\".\nDo you want to change the configuration ?", "Error",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Error) == DialogResult.OK)

                {
                    formMain.optionsform.bRDPath_Click(serverElement.Type);
                }
            }
        }

        private static void LaunchVnc(ServerElement serverElement)
        {
            string[] vncextractpath = ExtractFilePath(Settings.Default.vncpath);
            string vncpath = vncextractpath[0];
            string vncargs = vncextractpath[1];

            if (File.Exists(vncpath))
            {
                string host;
                string port;
                string[] hostport = serverElement.HostWithServer.Split(':');
                int split = hostport.Length;

                if (split == 2)
                {
                    host = hostport[0];
                    port = hostport[1];
                }
                else
                {
                    host = serverElement.Host;
                    port = "5900";
                }

                string vncout = "";

                if (Settings.Default.vncfilespath != "" && otherHelper.ReplaceA(ps, pr, Settings.Default.vncfilespath) != "\\")
                {
                    vncout = otherHelper.ReplaceA(ps, pr, Settings.Default.vncfilespath + "\\");

                    try
                    {
                        Directory.CreateDirectory(vncout);
                    }
                    catch
                    {
                        MessageBox.Show("Output path for generated \".vnc\" connection files doesn't exist.\nFiles will be generated in the current path.", StringResources.connectionHelper_Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        vncout = "";
                    }
                }

                TextWriter vncfile = new StreamWriter(vncout + otherHelper.ReplaceU(f, serverElement.Name.ToString()) + ".vnc");
                vncfile.WriteLine("[Connection]");
                if (host != "") vncfile.WriteLine("host=" + host.Trim());
                if (port != "") vncfile.WriteLine("port=" + port.Trim());
                if (serverElement.Username != "") vncfile.WriteLine("username=" + serverElement.Username);
                if (serverElement.Password != "") vncfile.WriteLine("password=" + cryptVNC.EncryptPassword(serverElement.Password));
                vncfile.WriteLine("[Options]");
                if (Settings.Default.vncfullscreen) vncfile.WriteLine("fullscreen=1");
                if (Settings.Default.vncviewonly)
                {
                    vncfile.WriteLine("viewonly=1"); //ultravnc
                    vncfile.WriteLine("sendptrevents=0"); //realvnc
                    vncfile.WriteLine("sendkeyevents=0"); //realvnc
                    vncfile.WriteLine("sendcuttext=0"); //realvnc
                    vncfile.WriteLine("acceptcuttext=0"); //realvnc
                    vncfile.WriteLine("sharefiles=0"); //realvnc
                }

                if (serverElement.Password != "" && serverElement.Password.Length > 8) vncfile.WriteLine("protocol3.3=1"); // fuckin vnc 4.0 auth
                vncfile.Close();

                Process myProc = new Process();
                myProc.StartInfo.FileName = Settings.Default.vncpath;
                myProc.StartInfo.Arguments = "-config \"" + vncout + otherHelper.ReplaceU(f, serverElement.Name.ToString()) + ".vnc\"";
                if (vncargs != "") myProc.StartInfo.Arguments += " " + vncargs;
                try
                {
                    myProc.Start();
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    //user canceled
                }
            }
            else
            {
                if (MessageBox.Show("Could not find file \"" + vncpath + "\".\nDo you want to change the configuration ?", "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error) == DialogResult.OK) formMain.optionsform.bVNCPath_Click(serverElement.Type);
            }
        }

        private static void LaunchPuTTy(ServerElement serverElement)
        {
            string[] puttyextractpath = ExtractFilePath(Settings.Default.puttypath);
            string puttypath = puttyextractpath[0];
            string puttyargs = puttyextractpath[1];
            // for some reason you only have escape \ if it's followed by "
            // will "fix" up to 3 \ in a password like \\\", then screw you with your maniac passwords
            string[] passs = { "\"", "\\\\\"", "\\\\\\\\\"", "\\\\\\\\\\\\\"", };
            string[] passr = { "\\\"", "\\\\\\\"", "\\\\\\\\\\\"", "\\\\\\\\\\\\\\\"", };

            if (File.Exists(puttypath))
            {
                string host;
                string port;
                string[] hostport = serverElement.HostWithServer.Split(':');
                int split = hostport.Length;

                if (split == 2)
                {
                    host = hostport[0];
                    port = hostport[1];
                }
                else
                {
                    host = serverElement.Host;
                    port = "";
                }

                Process myProc = new Process();
                myProc.StartInfo.FileName = Settings.Default.puttypath;
                myProc.StartInfo.Arguments = "-ssh ";
                if (serverElement.Username != "") myProc.StartInfo.Arguments += serverElement.Username + "@";
                if (host != "") myProc.StartInfo.Arguments += host;
                if (port != "") myProc.StartInfo.Arguments += " " + port;
                if (serverElement.Username != "" && serverElement.Password != "") myProc.StartInfo.Arguments += " -pw \"" + otherHelper.ReplaceA(passs, passr, serverElement.Password) + "\"";
                if (Settings.Default.puttyexecute && Settings.Default.puttycommand != "") myProc.StartInfo.Arguments += " -m \"" + Settings.Default.puttycommand + "\"";
                if (Settings.Default.puttykey && Settings.Default.puttykeyfile != "") myProc.StartInfo.Arguments += " -i \"" + Settings.Default.puttykeyfile + "\"";
                if (Settings.Default.puttyforward) myProc.StartInfo.Arguments += " -X";
                //MessageBox.Show(this, myProc.StartInfo.Arguments);
                if (puttyargs != "") myProc.StartInfo.Arguments += " " + puttyargs;
                try
                {
                    myProc.Start();
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    //user canceled
                }
            }
            else
            {
                if (MessageBox.Show("Could not find file \"" + puttypath + "\".\nDo you want to change the configuration ?", "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error) == DialogResult.OK) formMain.optionsform.bPuTTYPath_Click(serverElement.Type);
            }
        }

        private static void LaunchWinScp(string protocol, ServerElement serverElement)
        {
            string[] winscpextractpath = ExtractFilePath(Settings.Default.winscppath);
            string winscppath = winscpextractpath[0];
            string winscpargs = winscpextractpath[1];

            if (File.Exists(winscppath))
            {
                string host;
                string port;
                string[] hostport = serverElement.HostWithServer.Split(':');
                int split = hostport.Length;

                if (split == 2)
                {
                    host = hostport[0];
                    port = hostport[1];
                }
                else
                {
                    host = serverElement.Host;
                    port = "";
                }

                Process myProc = new Process();
                myProc.StartInfo.FileName = Settings.Default.winscppath;
                myProc.StartInfo.Arguments = protocol;
                if (serverElement.Username != "")
                {
                    string[] s = { "%", " ", "+", "/", "@", "\"", ":", ";" };
                    serverElement.Username = otherHelper.ReplaceU(s, serverElement.Username);
                    serverElement.Password = otherHelper.ReplaceU(s, serverElement.Password);
                    myProc.StartInfo.Arguments += serverElement.Username;
                    if (serverElement.Password != "") myProc.StartInfo.Arguments += ":" + serverElement.Password;
                    myProc.StartInfo.Arguments += "@";
                }
                if (host != "") myProc.StartInfo.Arguments += HttpUtility.UrlEncode(host) ?? throw new InvalidOperationException();
                if (port != "") myProc.StartInfo.Arguments += ":" + port;
                if (protocol == "ftp://") myProc.StartInfo.Arguments += " /passive=" + (Settings.Default.winscppassive ? "on" : "off");
                if (Settings.Default.winscpkey && Settings.Default.winscpkeyfile != "") myProc.StartInfo.Arguments += " /privatekey=\"" + Settings.Default.winscpkeyfile + "\"";
                if (winscpargs != "") myProc.StartInfo.Arguments += " " + winscpargs;
                try
                {
                    myProc.Start();
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    //user canceled
                }
            }
            else
            {
                if (MessageBox.Show("Could not find file \"" + winscppath + "\".\nDo you want to change the configuration ?", "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error) == DialogResult.OK) formMain.optionsform.bWSCPPath_Click(serverElement.Type);
            }
        }

        private static string[] ExtractFilePath(string path)
        {
            //extract file path and arguments
            if (path.IndexOf("\"", StringComparison.Ordinal) == 0)
            {
                int s = path.Substring(1).IndexOf("\"", StringComparison.Ordinal);
                if (s > 0) return new string[] { path.Substring(1, s), path.Substring(s + 2).Trim() };
                return new string[] { path.Substring(1), "" };
            }
            else
            {
                int s = path.Substring(1).IndexOf(" ", StringComparison.Ordinal);
                if (s > 0) return new string[] { path.Substring(0, s + 1), path.Substring(s + 2).Trim() };
                return new string[] { path.Substring(0), "" };
            }
        }
    }
}
