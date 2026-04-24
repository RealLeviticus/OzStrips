using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Timers;
using MaxRumsey.OzStripsPlugin.GUI.Properties;
using MaxRumsey.OzStripsPlugin.GUI.Shared;
using vatsys;

using static vatsys.FDP2;

namespace MaxRumsey.OzStripsPlugin.GUI;

/// <summary>
/// Bridges the plugin's strip data to the embedded web server by periodically broadcasting state.
/// </summary>
public sealed class WebViewerBridge : IDisposable
{
    private readonly EmbeddedWebServer _server;
    private readonly Timer _broadcastTimer;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebViewerBridge"/> class.
    /// </summary>
    /// <param name="port">The port to serve on.</param>
    public WebViewerBridge(int port = 5199)
    {
        _server = new EmbeddedWebServer(port);
        _server.OnClientMessage += HandleClientMessage;

        _broadcastTimer = new Timer(500);
        _broadcastTimer.Elapsed += (_, __) => BroadcastCurrentState();
        _broadcastTimer.AutoReset = true;
    }

    /// <summary>
    /// Starts the web server and broadcast timer.
    /// </summary>
    public void Start()
    {
        _server.Start();
        _broadcastTimer.Start();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _broadcastTimer.Stop();
        _broadcastTimer.Dispose();
        _server.Dispose();
    }

    [SuppressMessage(
        "Maintainability",
        "CA1505:Avoid unmaintainable code",
        Justification = "Command routing intentionally mirrors the web protocol action surface.")]
    private void HandleClientMessage(string message)
    {
        try
        {
            var cmd = System.Text.Json.JsonSerializer.Deserialize<WebCommand>(message);
            if (cmd is null)
            {
                return;
            }

            var controller = MainFormController.Instance;
            if (controller is null || controller.IsDisposed || !controller.Visible)
            {
                return;
            }

            var bayManager = controller.BayManager;
            if (bayManager is null)
            {
                return;
            }

            Strip? strip = null;
            if (!string.IsNullOrWhiteSpace(cmd.Callsign))
            {
                strip = bayManager.StripRepository.GetStrip(cmd.Callsign);
            }

            switch (cmd.Action)
            {
                case "setAerodrome":
                    if (!string.IsNullOrWhiteSpace(cmd.Value))
                    {
                        var aerodrome = cmd.Value.Trim().ToUpper(CultureInfo.InvariantCulture);
                        if (aerodrome.Length == 4)
                        {
                            controller.Invoke(() => controller.SetAerodrome(aerodrome));
                        }
                    }

                    break;

                case "setSmartResize":
                    if (!string.IsNullOrWhiteSpace(cmd.Value) &&
                        int.TryParse(cmd.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resizeMode) &&
                        resizeMode is >= 0 and <= 3)
                    {
                        controller.Invoke(() => controller.SetSmartResizeColumnMode(resizeMode));
                    }

                    break;

                case "toggleCircuit":
                    controller.Invoke(() => controller.ToggleCircuitBay(controller, EventArgs.Empty));
                    break;

                case "toggleCoordinator":
                    controller.Invoke(() => controller.ToggleCoordBay(controller, EventArgs.Empty));
                    break;

                case "toggleCdm":
                    controller.Invoke(() => controller.ToggleCDM(controller, EventArgs.Empty));
                    break;

                case "setLayout":
                    if (!string.IsNullOrWhiteSpace(cmd.Value))
                    {
                        controller.Invoke(() =>
                        {
                            var mf = MainForm.MainFormInstance;
                            if (mf is null || mf.IsDisposed)
                            {
                                return;
                            }

                            var items = mf.ViewListToolStrip.DropDownItems;
                            for (var i = 0; i < items.Count; i++)
                            {
                                if (items[i] is System.Windows.Forms.ToolStripMenuItem menuItem &&
                                    string.Equals(menuItem.Text, cmd.Value, StringComparison.OrdinalIgnoreCase))
                                {
                                    menuItem.PerformClick();
                                    break;
                                }
                            }
                        });
                    }

                    break;

                case "togglePdcSound":
                    controller.Invoke(() => controller.PDCSoundToggle(controller, EventArgs.Empty));
                    break;

                case "toggleGroundMaps":
                    controller.Invoke(() =>
                    {
                        var mainForm = MainForm.MainFormInstance;
                        if (mainForm is null || mainForm.IsDisposed)
                        {
                            return;
                        }

                        mainForm.InhibitGroundMaps.PerformClick();
                    });
                    break;

                case "openSettings":
                    controller.Invoke(() => controller.ShowSettings(controller, EventArgs.Empty));
                    break;

                case "openKeySettings":
                    controller.Invoke(() => controller.ShowKeySettings(controller, EventArgs.Empty));
                    break;

                case "showAbout":
                    controller.Invoke(() =>
                    {
                        var mainForm = MainForm.MainFormInstance;
                        if (mainForm is null || mainForm.IsDisposed)
                        {
                            return;
                        }

                        var modalChild = new MaxRumsey.OzStripsPlugin.GUI.Controls.About();
                        var bm = new BaseModal(modalChild, "About OzStrips");
                        bm.Show(mainForm);
                    });
                    break;

                case "showSignalRLog":
                    controller.Invoke(() => controller.ShowMessageList_Click(controller, EventArgs.Empty));
                    break;

                case "reloadStripElements":
                    controller.Invoke(MaxRumsey.OzStripsPlugin.GUI.DTO.StripElementList.Load);
                    break;

                case "reloadAerodromeList":
                    controller.Invoke(() =>
                    {
                        var mainForm = MainForm.MainFormInstance;
                        if (mainForm is null || mainForm.IsDisposed)
                        {
                            return;
                        }

                        mainForm.AerodromeManager.LoadSettings();
                    });
                    break;

                case "overrideAtis":
                    controller.Invoke(() => controller.OverrideATISClick(controller, EventArgs.Empty));
                    break;

                case "move":
                    if (strip is not null && cmd.Bay.HasValue)
                    {
                        controller.Invoke(() =>
                        {
                            var targetBay = FindBayByType(bayManager, cmd.Bay.Value);
                            if (targetBay is not null)
                            {
                                bayManager.MoveStrip(targetBay, strip);
                            }
                        });
                    }

                    break;

                case "moveUp":
                    if (strip is not null)
                    {
                        controller.Invoke(() => MoveStripRelative(bayManager, strip, 1));
                    }

                    break;

                case "moveDown":
                    if (strip is not null)
                    {
                        controller.Invoke(() => MoveStripRelative(bayManager, strip, -1));
                    }

                    break;

                case "moveToBarUp":
                    if (strip is not null)
                    {
                        controller.Invoke(() => MoveStripToNearestBar(bayManager, strip, 1));
                    }

                    break;

                case "moveToBarDown":
                    if (strip is not null)
                    {
                        controller.Invoke(() => MoveStripToNearestBar(bayManager, strip, -1));
                    }

                    break;

                case "cock":
                    if (strip is not null)
                    {
                        controller.Invoke(() => strip.CockStrip());
                    }

                    break;

                case "ready":
                    if (strip is not null)
                    {
                        controller.Invoke(() => strip.Controller.ToggleReady());
                    }

                    break;

                case "sidTrigger":
                    if (strip is not null)
                    {
                        controller.Invoke(() => strip.SIDTrigger());
                    }

                    break;

                case "toggleTot":
                    if (strip is not null)
                    {
                        controller.Invoke(() => strip.TakeOff());
                    }

                    break;

                case "assignSsr":
                    if (strip is not null)
                    {
                        controller.Invoke(() => strip.Controller.AssignSSR());
                    }

                    break;

                case "openFdr":
                    if (strip is not null)
                    {
                        controller.Invoke(() => strip.OpenVatsysFDR());
                    }

                    break;

                case "showRoute":
                    if (strip is not null)
                    {
                        controller.Invoke(() =>
                        {
                            var track = MMI.FindTrack(strip.FDR);
                            if (track is null)
                            {
                                return;
                            }

                            if (track.GraphicRTE)
                            {
                                MMI.HideGraphicRoute(track);
                            }
                            else
                            {
                                MMI.ShowGraphicRoute(track);
                            }
                        });
                    }

                    break;

                case "openCdm":
                    if (strip is not null)
                    {
                        controller.Invoke(() => strip.Controller.OpenCDM());
                    }

                    break;

                case "openPdc":
                    if (strip is not null)
                    {
                        controller.Invoke(() =>
                        {
                            if (strip.PDCRequest?.Flags.HasFlag(PDCRequest.PDCFlags.REQUESTED) == true &&
                                strip.StripType != StripType.ARRIVAL)
                            {
                                if (!strip.PDCRequest.Flags.HasFlag(PDCRequest.PDCFlags.ACKNOWLEDGED))
                                {
                                    strip.PDCFlags |= PDCRequest.PDCFlags.ACKNOWLEDGED;
                                    strip.SyncStrip();
                                }

                                strip.Controller.OpenPDCWindow();
                                return;
                            }

                            strip.Controller.OpenVatSysPDCWindow();
                        });
                    }

                    break;

                case "openPm":
                    if (strip is not null)
                    {
                        controller.Invoke(() => MMI.OpenPMWindow(strip.FDR.Callsign));
                    }

                    break;

                case "openReroute":
                    if (strip is not null)
                    {
                        controller.Invoke(() => strip.Controller.OpenRerouteMenu());
                    }

                    break;

                case "crossing":
                    if (strip is not null)
                    {
                        controller.Invoke(() =>
                        {
                            strip.Crossing = !strip.Crossing;
                            strip.Controller.SetCross();
                        });
                    }

                    break;

                case "inhibitAlert":
                    if (strip is not null && !string.IsNullOrWhiteSpace(cmd.Value))
                    {
                        controller.Invoke(() =>
                        {
                            var alert = ResolveAlertType(cmd.Value);
                            if (!alert.HasValue)
                            {
                                return;
                            }

                            if (strip.IsAlertActive(alert.Value))
                            {
                                strip.InhibitAlert(alert.Value);
                            }
                        });
                    }

                    break;

                case "inhibitSidAlert":
                    if (strip is not null)
                    {
                        controller.Invoke(() =>
                        {
                            if (strip.IsAlertActive(MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.VFR_SID))
                            {
                                strip.InhibitAlert(MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.VFR_SID);
                            }
                        });
                    }

                    break;

                case "inhibit":
                    if (strip is not null)
                    {
                        controller.Invoke(() =>
                        {
                            strip.CurrentBay = StripBay.BAY_DEAD;
                            strip.SyncStrip();
                            bayManager.UpdateBay(strip);
                            bayManager.RemovePicked(true, true);
                        });
                    }

                    break;

                case "flip":
                    if (strip is not null)
                    {
                        controller.Invoke(() => strip.FlipFlop());
                    }

                    break;

                case "setClx":
                    if (strip is not null && cmd.Value is not null)
                    {
                        controller.Invoke(() =>
                        {
                            strip.CLX = cmd.Value;
                            strip.SyncStrip();
                        });
                    }

                    break;

                case "setRemark":
                    if (strip is not null && cmd.Value is not null)
                    {
                        controller.Invoke(() =>
                        {
                            strip.Remark = cmd.Value;
                            strip.SyncStrip();
                        });
                    }

                    break;

                case "setGate":
                    if (strip is not null && cmd.Value is not null)
                    {
                        controller.Invoke(() =>
                        {
                            strip.Gate = cmd.Value;
                            strip.SyncStrip();
                        });
                    }

                    break;

                case "setFreq":
                    if (strip is not null && cmd.Value is not null)
                    {
                        controller.Invoke(() =>
                        {
                            strip.DepartureFrequency = cmd.Value;
                            strip.SyncStrip();
                        });
                    }

                    break;

                case "setCfl":
                    if (strip is not null && cmd.Value is not null)
                    {
                        controller.Invoke(() => strip.CFL = cmd.Value);
                    }

                    break;

                case "setRwy":
                    if (strip is not null && cmd.Value is not null)
                    {
                        controller.Invoke(() => strip.RWY = cmd.Value);
                    }

                    break;

                case "setSid":
                    if (strip is not null && cmd.Value is not null)
                    {
                        controller.Invoke(() => strip.SID = cmd.Value);
                    }

                    break;

                case "setGlop":
                    if (strip is not null && cmd.Value is not null)
                    {
                        controller.Invoke(() =>
                        {
                            if (Network.Me.IsRealATC || MainForm.IsDebug)
                            {
                                SetGlobalOps(strip.FDR, cmd.Value);
                            }
                        });
                    }

                    break;

                case "queueUp":
                    if (strip is not null)
                    {
                        controller.Invoke(() =>
                        {
                            var sourceBay = bayManager.BayRepository.FindBay(strip);
                            if (sourceBay is null)
                            {
                                return;
                            }

                            sourceBay.AddDivider(true, false);
                            var stripItem = sourceBay.GetListItem(strip);
                            if (stripItem is null)
                            {
                                return;
                            }

                            sourceBay.ChangeStripPositionAbs(stripItem, sourceBay.DivPosition);
                            bayManager.RemovePicked(true);
                        });
                    }

                    break;

                case "toggleQueueBar":
                    if (cmd.Bay.HasValue)
                    {
                        controller.Invoke(() =>
                        {
                            var targetBay = FindBayByType(bayManager, cmd.Bay.Value);
                            targetBay?.AddDivider(false);
                        });
                    }

                    break;

                case "addBar":
                    if (cmd.Bay.HasValue && !string.IsNullOrWhiteSpace(cmd.Value))
                    {
                        controller.Invoke(() =>
                        {
                            var targetBay = FindBayByType(bayManager, cmd.Bay.Value);
                            if (targetBay is null)
                            {
                                return;
                            }

                            var style = cmd.Style ?? 1;
                            if (style is < 1 or > 3)
                            {
                                style = 1;
                            }

                            targetBay.AddBar(style, cmd.Value.Trim());
                        });
                    }

                    break;

                case "toggleCrossBar":
                case "toggleReleaseBar":
                    if (!string.IsNullOrWhiteSpace(cmd.Value))
                    {
                        controller.Invoke(() =>
                        {
                            var runwayPair = NormalizeRunwayPair(cmd.Value);
                            if (runwayPair.Length < 2 || runwayPair.Length % 2 != 0)
                            {
                                return;
                            }

                            bayManager.ToggleCrossReleaseBar(
                                runwayPair,
                                cmd.Action == "toggleReleaseBar" ? "Released" : "Crossing");
                        });
                    }

                    break;

                case "toggleCrossBarDirect":
                    controller.Invoke(controller.ToggleCrossBar);
                    break;

                case "toggleReleaseBarDirect":
                    controller.Invoke(controller.ToggleReleaseBar);
                    break;
            }
        }
        catch
        {
        }
    }

    private void BroadcastCurrentState()
    {
        try
        {
            var controller = MainFormController.Instance;
            if (controller is null || controller.IsDisposed || !controller.Visible)
            {
                return;
            }

            BayManager bayManager;
            SocketConn socketConn;
            string atis;
            string metar;

            try
            {
                bayManager = controller.BayManager;
                socketConn = controller.SocketConn;
                metar = controller.MetarString ?? string.Empty;
                atis = controller.AtisCode ?? string.Empty;
            }
            catch
            {
                return;
            }

            if (bayManager is null || socketConn is null)
            {
                return;
            }

            var mainForm = MainForm.MainFormInstance;
            var circuitToggleAvailable = false;
            if (mainForm is not null)
            {
                var isRadarTower = string.IsNullOrEmpty(mainForm.AerodromeManager.GetAerodromeType(bayManager.AerodromeName));
                circuitToggleAvailable = isRadarTower && socketConn.HaveSendPerms;
            }

            var stripScale = OzStripsSettings.Default.StripScale;
            if (stripScale < 1f)
            {
                stripScale = 1f;
            }
            else if (stripScale > 3f)
            {
                stripScale = 3f;
            }

            var state = new WebViewerState
            {
                Aerodrome = bayManager.AerodromeName,
                Atis = atis,
                Metar = metar,
                Connected = socketConn.Connected,
                UtcTime = DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                ConnectionsCount = bayManager.AerodromeState.Connections?.Count ?? 0,
                PendingPDCs = CountPendingPDCs(bayManager),
                AutoFillAvailable = bayManager.AutoAssigner?.IsFunctional ?? false,
                StripScale = stripScale,
                RunwayPairs = GetRunwayPairsForAerodrome(bayManager.AerodromeName),
                SmartResizeMode = OzStripsSettings.Default.SmartResize,
                CircuitActive = bayManager.CircuitActive,
                CoordinatorActive = bayManager.CoordinatorBayActive,
                CdmEnabled = bayManager.AerodromeState.CDMParameters.Enabled ?? false,
                PdcSoundEnabled = mainForm?.PDCSoundMenuItem.Checked ?? true,
                GroundMapsInhibited = mainForm?.InhibitGroundMaps.Checked ?? false,
                CircuitToggleAvailable = circuitToggleAvailable,
            };

            // Populate available view mode layouts
            try
            {
                if (mainForm is not null)
                {
                    var aerodromeType = mainForm.AerodromeManager.GetAerodromeType(bayManager.AerodromeName);
                    var layouts = mainForm.AerodromeManager.ReturnLayouts(aerodromeType);
                    state.Layouts = layouts.Select(l => l.Name).ToList();
                }
            }
            catch
            {
                // Layouts may not be available yet
            }

            state.CurrentLayout = controller.LayoutName;

            Bay[] baySnapshot;
            try
            {
                baySnapshot = bayManager.BayRepository.Bays.ToArray();
            }
            catch
            {
                return;
            }

            foreach (var bay in baySnapshot)
            {
                if (bay.BayTypes.Count == 0)
                {
                    continue;
                }

                var webBay = new WebViewerBay
                {
                    Bay = bay.BayTypes[0],
                    Name = bay.Name,
                    Column = bay.LayoutColumn,
                };

                StripListItem[] items;
                try
                {
                    items = bay.Strips.ToArray();
                }
                catch
                {
                    continue;
                }

                var legacyStrips = new List<WebViewerStrip>();
                var queueBarIndex = -1;

                for (var i = items.Length - 1; i >= 0; i--)
                {
                    var item = items[i];
                    switch (item.Type)
                    {
                        case StripItemType.STRIP when item.Strip is not null:
                        {
                            var strip = BuildWebStrip(item.Strip, bayManager);
                            webBay.Items.Add(new WebViewerBayItem
                            {
                                ItemType = "STRIP",
                                Strip = strip,
                            });

                            legacyStrips.Add(strip);
                            break;
                        }

                        case StripItemType.QUEUEBAR:
                        {
                            webBay.Items.Add(new WebViewerBayItem
                            {
                                ItemType = "QUEUEBAR",
                                Text = item.BarText ?? "Queue (0)",
                                Style = 0,
                            });

                            queueBarIndex = legacyStrips.Count;
                            break;
                        }

                        case StripItemType.BAR:
                        {
                            webBay.Items.Add(new WebViewerBayItem
                            {
                                ItemType = "BAR",
                                Text = item.BarText ?? string.Empty,
                                Style = item.Style ?? 0,
                            });

                            break;
                        }
                    }
                }

                webBay.Strips = legacyStrips;
                webBay.QueueBarIndex = queueBarIndex;
                state.Bays.Add(webBay);
            }

            _server.BroadcastState(state);
        }
        catch
        {
        }
    }

    private static WebViewerStrip BuildWebStrip(Strip strip, BayManager bayManager)
    {
        var fdr = strip.FDR;

        var ssrCode = fdr.AssignedSSRCode == -1
            ? "XXXX"
            : Convert.ToString(fdr.AssignedSSRCode, 8).PadLeft(4, '0');

        var routeIndicator = string.Empty;
        if (fdr.TextOnly)
        {
            routeIndicator = "T";
        }
        else if (fdr.ReceiveOnly)
        {
            routeIndicator = "R";
        }

        var firstWpt = strip.FirstWpt ?? string.Empty;
        if (firstWpt.Length > 5)
        {
            firstWpt = firstWpt.Substring(0, 5) + "...";
        }

        var cfl = strip.CFL ?? string.Empty;
        if (cfl.Contains("B"))
        {
            cfl = "BLK";
        }

        var standAutofilled = !string.IsNullOrEmpty(strip.AllocatedBay) && string.IsNullOrEmpty(strip.Gate);
        var stand = standAutofilled ? strip.AllocatedBay : strip.Gate;

        var pdcRequestedUnsent = false;
        var pdcNeedsAck = false;
        var pdcIndicator = string.Empty;

        if (strip.FDR.PDCSent || strip.PDCFlags.HasFlag(PDCRequest.PDCFlags.SENT))
        {
            pdcIndicator = "P";
        }

        if (strip.StripType != StripType.ARRIVAL)
        {
            var requestedPdc = strip.PDCRequest;
            pdcRequestedUnsent = requestedPdc is not null &&
                                 requestedPdc.Flags.HasFlag(PDCRequest.PDCFlags.REQUESTED) &&
                                 !strip.PDCFlags.HasFlag(PDCRequest.PDCFlags.SENT);
            pdcNeedsAck = pdcRequestedUnsent && !strip.PDCFlags.HasFlag(PDCRequest.PDCFlags.ACKNOWLEDGED);
        }

        var cdmResult = strip.CDMResult;
        var cdmActive = cdmResult is not null && cdmResult.Aircraft.State != CDMState.PUSHED;

        var takeoffTimer = "00:00";
        if (strip.TakeOffTime is not null)
        {
            var diff = DateTime.UtcNow - strip.TakeOffTime.Value;
            takeoffTimer = diff.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        return new WebViewerStrip
        {
            Callsign = fdr.Callsign,
            CLX = strip.CLX ?? string.Empty,
            Gate = stand ?? string.Empty,
            Remark = strip.Remark ?? string.Empty,
            DepartureFreq = strip.DepartureFrequency ?? string.Empty,
            Runway = strip.RWY ?? string.Empty,
            CockLevel = strip.CockLevel,
            Bay = strip.CurrentBay,
            StripType = strip.StripType.ToString(),
            Time = strip.Time ?? string.Empty,
            AircraftType = fdr.AircraftType ?? string.Empty,
            WTC = fdr.AircraftWake ?? string.Empty,
            SSR = ssrCode,
            ADES = fdr.DesAirport ?? string.Empty,
            SID = strip.SID ?? string.Empty,
            FirstWpt = firstWpt,
            RFL = strip.RFL ?? string.Empty,
            CFL = cfl,
            GLOP = fdr.GlobalOpData ?? string.Empty,
            FlightRules = fdr.FlightRules ?? string.Empty,
            Ready = strip.Ready,
            RouteIndicator = routeIndicator,
            AllocatedBay = strip.AllocatedBay ?? string.Empty,
            Crossing = strip.Crossing,
            HDG = strip.HDG ?? string.Empty,
            Tot = takeoffTimer,
            PDCIndicator = pdcIndicator,
            PdcRequested = pdcRequestedUnsent,
            PdcNeedsAck = pdcNeedsAck,
            SidTransition = strip.SIDTransition is not null,
            RouteAlert = strip.IsAlertActive(MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.ROUTE),
            RflAlert = strip.IsAlertActive(MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.RFL),
            SsrAlert = strip.IsAlertActive(MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.SSR),
            ReadyAlert = strip.IsAlertActive(MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.READY),
            NoHdgAlert = strip.IsAlertActive(MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.NO_HDG),
            VfrSidAlert = strip.IsAlertActive(MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.VFR_SID),
            CdmActive = cdmActive,
            CdmReadyToPush = cdmActive && strip.ReadyToPush,
            CdmHasSlot = cdmActive && cdmResult?.Slot is not null,
            StandAutofilled = standAutofilled,
            Picked = bayManager.PickedStrip == strip,
            LastTransmit = bayManager.LastTransmitManager.LastReceivedFrom == fdr.Callsign && OzStripsSettings.Default.ShowLastTransmit,
            WorldFlight = bayManager.AerodromeState.WorldFlightTeams.Contains(fdr.Callsign),
        };
    }

    private static void MoveStripRelative(BayManager bayManager, Strip strip, int direction)
    {
        var bay = bayManager.BayRepository.FindBay(strip);
        if (bay is null)
        {
            return;
        }

        var item = bay.GetListItem(strip);
        if (item is null)
        {
            return;
        }

        bay.ChangeStripPosition(item, direction);
    }

    private static void MoveStripToNearestBar(BayManager bayManager, Strip strip, int direction)
    {
        var bay = bayManager.BayRepository.FindBay(strip);
        if (bay is null)
        {
            return;
        }

        var item = bay.GetListItem(strip);
        if (item is null)
        {
            return;
        }

        var index = bay.Strips.IndexOf(item);
        if (index == -1)
        {
            return;
        }

        var neighbour = bay.Strips.ElementAtOrDefault(index + direction);
        if (neighbour?.Type != StripItemType.STRIP)
        {
            bay.ChangeStripPosition(item, direction);
            return;
        }

        if (direction > 0)
        {
            for (var i = index + 2; i < bay.Strips.Count; i++)
            {
                var present = bay.Strips.ElementAtOrDefault(i);
                if (present is not null && present.Type != StripItemType.STRIP)
                {
                    bay.ChangeStripPositionAbs(item, i - 1);
                    return;
                }
            }
        }
        else
        {
            for (var i = index - 2; i >= 0; i--)
            {
                var present = bay.Strips.ElementAtOrDefault(i);
                if (present is not null && present.Type != StripItemType.STRIP)
                {
                    bay.ChangeStripPositionAbs(item, i + 1);
                    return;
                }
            }
        }
    }

    private static MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes? ResolveAlertType(string value)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "route":
                return MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.ROUTE;
            case "rfl":
                return MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.RFL;
            case "ssr":
                return MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.SSR;
            case "ready":
                return MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.READY;
            case "sid":
                return MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.VFR_SID;
            case "hdg":
                return MaxRumsey.OzStripsPlugin.GUI.Shared.AlertTypes.NO_HDG;
            default:
                return null;
        }
    }

    private static int CountPendingPDCs(BayManager bayManager)
    {
        try
        {
            return bayManager.AerodromeState.PDCRequests.Count(x =>
            {
                var strip = bayManager.StripRepository.GetStrip(x.Callsign);
                if (strip is null)
                {
                    return false;
                }

                return x.Flags.HasFlag(PDCRequest.PDCFlags.REQUESTED) &&
                    !strip.PDCFlags.HasFlag(PDCRequest.PDCFlags.SENT);
            });
        }
        catch
        {
            return 0;
        }
    }

    private static List<string> GetRunwayPairsForAerodrome(string aerodrome)
    {
        try
        {
            var mainForm = MainForm.MainFormInstance;
            var autoMap = mainForm?.AerodromeManager?.Settings?.AutoMapAerodromes?.FirstOrDefault(x => x.ICAOCode == aerodrome);
            if (autoMap?.RunwayPairs is null)
            {
                return new List<string>();
            }

            return autoMap.RunwayPairs
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpper(CultureInfo.InvariantCulture))
                .Distinct()
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static Bay? FindBayByType(BayManager bayManager, StripBay bayType)
    {
        foreach (var bay in bayManager.BayRepository.Bays)
        {
            if (bay.BayTypes.Contains(bayType))
            {
                return bay;
            }
        }

        return null;
    }

    private static string NormalizeRunwayPair(string pair)
    {
        if (pair is null)
        {
            return string.Empty;
        }

        var cleaned = pair
            .Replace("/", string.Empty)
            .Replace(" ", string.Empty)
            .Trim();

        return cleaned.ToUpper(CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Represents a command sent from the web client.
/// </summary>
public class WebCommand
{
    /// <summary>Gets or sets the action.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the callsign.</summary>
    public string Callsign { get; set; } = string.Empty;

    /// <summary>Gets or sets the target bay.</summary>
    public StripBay? Bay { get; set; }

    /// <summary>Gets or sets a string value for set operations.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional style/value numeric parameter.</summary>
    public int? Style { get; set; }
}
