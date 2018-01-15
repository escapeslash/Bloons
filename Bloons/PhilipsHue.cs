using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CocosSharp;
using Q42.HueApi;
using Q42.HueApi.ColorConverters.Original;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;

namespace Bloons
{
    public class PhilipsHue
    {
        bool isCompleted;
        bool isConnected;
        int bridgeLocatorTimeout = 30;
        string appName = string.Empty;
        string deviceName = string.Empty;
        string hueAppKey = string.Empty;
        string errorLog = string.Empty;
        ILocalHueClient hueClient;
        List<Light> colorBulbs = new List<Light>();

        public PhilipsHue()
        {
            appName = string.Empty;
            deviceName = string.Empty;
            hueAppKey = string.Empty;
            bridgeLocatorTimeout = 30;
        }

        public PhilipsHue(string appName, string deviceName, string hueAppKey, int bridgeLocatorTimeout)
        {
            this.appName = appName;
            this.deviceName = deviceName;
            this.hueAppKey = hueAppKey;
            this.bridgeLocatorTimeout = bridgeLocatorTimeout;
        }

        public string DeviceName
        {
            get
            {
                return deviceName;
            }
        }

        public string HueAppKey
        {
            get
            {
                return hueAppKey;
            }
        }

        public List<Light> ColorBulbs
        {
            get
            {
                return colorBulbs;
            }
            set
            {
                colorBulbs = value;
            }
        }

        public bool IsCompleted
        {
            get
            {
                return isCompleted;
            }
        }

        public bool IsConnected
        {
            get
            {
                return isConnected;
            }
        }

        public string ErrorLog
        {
            get
            {
                return errorLog;
            }
        }

        public void SetBulbColor(int redComponent, int greenComponent, int blueComponent, string bulbID = "", Action callbackMethod = null)
        {
            try
            {
                Task setColorTask = new Task(async () =>
                {
                    if ((hueClient != null) &&
                        (await hueClient.CheckConnection() == true))
                    {
                        if (colorBulbs.Count > 0)
                        {
                            List<string> bulbIDs = new List<string>();
                            LightCommand lightCommand = new LightCommand();

                            if (bulbID == string.Empty)
                            {
                                for (int i = 0; i < colorBulbs.Count; i++)
                                {
                                    bulbIDs.Add(colorBulbs[i].Id);
                                }
                            }
                            else
                            {
                                bulbIDs.Add(bulbID);
                            }

                            lightCommand.TurnOn();
                            lightCommand.SetColor(HueColorConverter.XyFromColor(redComponent, greenComponent, blueComponent).x,
                                                  HueColorConverter.XyFromColor(redComponent, greenComponent, blueComponent).y);

                            lightCommand.Brightness = 200;

                            await hueClient.SendCommandAsync(lightCommand, bulbIDs);
                        }

                        callbackMethod?.Invoke();
                    }
                    else
                    {
                        errorLog += "\r\n\r\n" + DateTime.Now.ToString() + ": Philips Hue bridge is not connected.";

                        callbackMethod?.Invoke();
                    }
                });

                setColorTask.Start();
            }
            catch (Exception setColorException)
            {
                errorLog += "\r\n\r\n" + DateTime.Now.ToString() + ": " + setColorException;

                callbackMethod?.Invoke();
            }
        }

        public void FlashLights(string bulbID = "", Action callbackMethod = null)
        {
            try
            {
                Task flashLightsTask = new Task(async () =>
                {
                    if ((hueClient != null) &&
                        (await hueClient.CheckConnection() == true))
                    {
                        if (colorBulbs.Count > 0)
                        {
                            List<string> bulbIDs = new List<string>();
                            LightCommand lightCommand = new LightCommand();

                            if (bulbID == string.Empty)
                            {
                                for (int i = 0; i < colorBulbs.Count; i++)
                                {
                                    bulbIDs.Add(colorBulbs[i].Id);
                                }
                            }
                            else
                            {
                                bulbIDs.Add(bulbID);
                            }

                            lightCommand.TurnOn();

                            lightCommand.Brightness = 255;

                            await hueClient.SendCommandAsync(lightCommand, bulbIDs);
                            await Task.Delay(100);

                            lightCommand.Brightness = 155;

                            await hueClient.SendCommandAsync(lightCommand, bulbIDs);
                        }

                        callbackMethod?.Invoke();
                    }
                    else
                    {
                        errorLog += "\r\n\r\n" + DateTime.Now.ToString() + ": Philips Hue bridge is not connected.";

                        callbackMethod?.Invoke();
                    }
                });

                flashLightsTask.Start();
            }
            catch (Exception lightEffectException)
            {
                errorLog += "\r\n\r\n" + DateTime.Now.ToString() + ": " + lightEffectException;

                callbackMethod?.Invoke();
            }
        }

        public void AlertLights(string bulbID = "", Action callbackMethod = null)
        {
            try
            {
                Task flashLightsTask = new Task(async () =>
                {
                    if ((hueClient != null) &&
                        (await hueClient.CheckConnection() == true))
                    {
                        if (colorBulbs.Count > 0)
                        {
                            List<string> bulbIDs = new List<string>();
                            LightCommand lightCommand = new LightCommand();

                            if (bulbID == string.Empty)
                            {
                                for (int i = 0; i < colorBulbs.Count; i++)
                                {
                                    bulbIDs.Add(colorBulbs[i].Id);
                                }
                            }
                            else
                            {
                                bulbIDs.Add(bulbID);
                            }

                            lightCommand.TurnOn();

                            lightCommand.Alert = Alert.Multiple;

                            await hueClient.SendCommandAsync(lightCommand, bulbIDs);
                        }

                        callbackMethod?.Invoke();
                    }
                    else
                    {
                        errorLog += "\r\n\r\n" + DateTime.Now.ToString() + ": Philips Hue bridge is not connected.";

                        callbackMethod?.Invoke();
                    }
                });

                flashLightsTask.Start();
            }
            catch (Exception lightEffectException)
            {
                errorLog += "\r\n\r\n" + DateTime.Now.ToString() + ": " + lightEffectException;

                callbackMethod?.Invoke();
            }
        }

        public void Connect(Action callbackMethod)
        {
            Task connectionTask;

            deviceName = (String.IsNullOrWhiteSpace(deviceName) ? "iOS_" + CCRandom.GetRandomInt(0, 100) : deviceName);
            isCompleted = false;
            isConnected = false;

            connectionTask = new Task(async () =>
            {
                int locatorTimeout = bridgeLocatorTimeout;
                Exception currentException = new Exception();
                IBridgeLocator bridgeLocator = new HttpBridgeLocator();
                IEnumerable<LocatedBridge> locatedBridges = new List<LocatedBridge>();

                try
                {
                    locatedBridges = await bridgeLocator.LocateBridgesAsync(TimeSpan.FromSeconds(locatorTimeout));

                    if (((List<LocatedBridge>)locatedBridges).Count > 0)
                    {
                        while (((currentException.Source == null) ||
                               (currentException.Message == "Link button not pressed")) &&
                               (locatorTimeout >= 0))
                        {
                            try
                            {
                                hueClient = new LocalHueClient(((List<LocatedBridge>)locatedBridges)[0].IpAddress);

                                if (hueClient != null)
                                {
                                    if (await hueClient.CheckConnection() == false)
                                    {
                                        if (String.IsNullOrWhiteSpace(hueAppKey) == true)
                                        {
                                            hueAppKey = await hueClient.RegisterAsync(appName, deviceName);
                                        }

                                        if (String.IsNullOrWhiteSpace(hueAppKey) == false)
                                        {
                                            hueClient.Initialize(hueAppKey);

                                            colorBulbs = ((List<Light>)await hueClient.GetLightsAsync());

                                            for (int i = 0; i < colorBulbs.Count; i++)
                                            {
                                                if (colorBulbs[i].Type != "Extended color light")
                                                {
                                                    colorBulbs.RemoveAt(i);

                                                    i--;
                                                }
                                            }

                                            isConnected = true;

                                            break;
                                        }
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            catch (Exception connectionException)
                            {
                                currentException = connectionException;
                            }

                            await Task.Delay(1000);

                            locatorTimeout--;
                        }
                    }
                }
                catch (Exception bridgeLocatorException)
                {
                    currentException = bridgeLocatorException;
                }

                isCompleted = true;

                callbackMethod();
            });

            connectionTask.Start();
        }

        public void Disconnect()
        {
            hueClient = null;
            appName = string.Empty;
            deviceName = string.Empty;
            hueAppKey = string.Empty;
            isCompleted = false;
            isConnected = false;
        }
    }
}