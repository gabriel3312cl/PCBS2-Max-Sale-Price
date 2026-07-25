using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCBS2MaxSalePrice
{
    public sealed class RemoteSaleController : MonoBehaviour
    {
        private const int StageNone = 0;
        private const int StageWaitForCarry = 1;
        private const int StageWaitForDestination = 2;
        private const int StageOpenPriceEditor = 3;

        private readonly List<BenchSlot> _saleSlots = new List<BenchSlot>();

        private bool _showPicker;
        private BenchSlot _sourceSlot;
        private BenchSlot _destinationSlot;
        private ComputerSave _pendingComputer;
        private WorkshopController _workshop;
        private WorkshopController _pickerWorkshop;
        private int _transferStage;
        private int _stageDeadlineFrame;
        private int _nextPriceOpenAttempt;

        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLock;
        private string _statusMessage;
        private float _statusUntil;

        public RemoteSaleController(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            try
            {
                if (_showPicker)
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    _pickerWorkshop?.DisableWalking();
                    _pickerWorkshop?.DisableMouseLook();
                }

                if (_showPicker && Input.GetKeyDown(KeyCode.Escape))
                    ClosePicker(true);

                if (!_showPicker && _transferStage == StageNone
                    && Input.GetKeyDown(Plugin.RemoteSaleKey.Value))
                    CapturePcAndOpenPicker();

                if (_transferStage != StageNone)
                    ProcessTransfer();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[RemoteSale] Update: " + e);
                FailTransfer("Error interno durante la venta remota.", true);
            }
        }

        private void CapturePcAndOpenPicker()
        {
            var source = FindPcSlotUnderCursor();
            if (source == null)
            {
                ShowStatus("No se encontró un PC bajo el cursor.");
                Plugin.Log.LogWarning("[RemoteSale] N: no hay PC bajo el cursor.");
                return;
            }

            RefreshSaleSlots();
            if (_saleSlots.Count == 0)
            {
                ShowStatus("No se encontraron mostradores de venta activos.");
                Plugin.Log.LogWarning("[RemoteSale] No hay BenchSlot FOR_SALE activos.");
                return;
            }

            _sourceSlot = source;
            _pickerWorkshop = WorkshopController.Get();
            _showPicker = true;
            _previousCursorVisible = Cursor.visible;
            _previousCursorLock = Cursor.lockState;

            if (_pickerWorkshop != null)
            {
                _pickerWorkshop.DisableWalking();
                _pickerWorkshop.DisableMouseLook();
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Plugin.Log.LogInfo($"[RemoteSale] PC capturado desde '{source.name}'. Mostradores={_saleSlots.Count}.");
        }

        private BenchSlot FindPcSlotUnderCursor()
        {
            var camera = Camera.main;
            if (camera == null) return null;

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 100f);
            BenchSlot best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; hits != null && i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null) continue;

                var slot = collider.GetComponentInParent<BenchSlot>();
                if (slot == null)
                {
                    var pcCase = collider.GetComponentInParent<Case>();
                    if (pcCase != null) slot = pcCase.slot;
                }

                if (!SlotHasPc(slot) || hits[i].distance >= bestDistance) continue;
                best = slot;
                bestDistance = hits[i].distance;
            }

            return best;
        }

        private void RefreshSaleSlots()
        {
            _saleSlots.Clear();
            var seen = new HashSet<int>();
            var slots = Resources.FindObjectsOfTypeAll<BenchSlot>();

            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.m_type != BenchSlot.Type.FOR_SALE) continue;
                if (slot.gameObject == null || !slot.gameObject.activeInHierarchy) continue;
                if (!seen.Add(slot.GetInstanceID())) continue;
                _saleSlots.Add(slot);
            }

            _saleSlots.Sort((a, b) => GetSaleDisplayId(a).CompareTo(GetSaleDisplayId(b)));
        }

        private void OnGUI()
        {
            try
            {
                DrawStatusMessage();
                if (!_showPicker) return;

                GUI.depth = -1000;
                float width = 460f;
                float rowHeight = 46f;
                float height = 105f + (_saleSlots.Count * rowHeight) + 56f;
                float left = (Screen.width - width) * 0.5f;
                float top = (Screen.height - height) * 0.5f;
                var panel = new Rect(left, top, width, height);

                GUI.Box(panel, string.Empty);
                GUI.Label(new Rect(left + 20f, top + 16f, width - 40f, 28f),
                    "VENTA REMOTA — SELECCIONA UN MOSTRADOR");
                GUI.Label(new Rect(left + 20f, top + 47f, width - 40f, 24f),
                    "El PC señalado se trasladará automáticamente.");

                float y = top + 80f;
                for (int i = 0; i < _saleSlots.Count; i++)
                {
                    var slot = _saleSlots[i];
                    bool free = slot != null && slot != _sourceSlot && IsSlotEmpty(slot);
                    int displayNumber = GetSaleDisplayId(slot) + 1;
                    string state = free ? "LIBRE" : "OCUPADO";

                    GUI.enabled = free;
                    if (GUI.Button(new Rect(left + 20f, y, width - 40f, 36f),
                        $"Mostrador {displayNumber} — {state}"))
                    {
                        GUI.enabled = true;
                        BeginTransfer(slot);
                        return;
                    }
                    GUI.enabled = true;
                    y += rowHeight;
                }

                if (GUI.Button(new Rect(left + 20f, top + height - 46f, width - 40f, 30f), "CANCELAR"))
                    ClosePicker(true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[RemoteSale] OnGUI: " + e);
            }
        }

        private void BeginTransfer(BenchSlot destination)
        {
            var source = _sourceSlot;
            ClosePicker(false);

            if (!SlotHasPc(source) || destination == null || !IsSlotEmpty(destination))
            {
                ShowStatus("El PC o el mostrador ya no están disponibles.");
                return;
            }

            var pc = source.GetComputer(true);
            var pcCase = source.caseInSlot;
            var status = source.m_caseStatus;
            var workshop = WorkshopController.Get();
            if (pc == null || pcCase == null || workshop == null)
            {
                ShowStatus("No se pudo preparar el PC para trasladarlo.");
                Plugin.Log.LogWarning("[RemoteSale] Faltan ComputerSave, Case o WorkshopController.");
                return;
            }

            _sourceSlot = source;
            _destinationSlot = destination;
            _pendingComputer = pc;
            _workshop = workshop;

            try
            {
                workshop.PickUpCase(pcCase, status);
                _transferStage = StageWaitForCarry;
                _stageDeadlineFrame = Time.frameCount + 240;
                ShowStatus("Trasladando PC al mostrador...", 6f);
                Plugin.Log.LogInfo($"[RemoteSale] PickUpCase: '{source.name}' -> mostrador {GetSaleDisplayId(destination) + 1}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[RemoteSale] PickUpCase: " + e);
                FailTransfer("No se pudo recoger el PC.", false);
            }
        }

        private void ProcessTransfer()
        {
            if (_transferStage == StageWaitForCarry)
            {
                var carried = _workshop != null ? _workshop.GetCarriedComputer() : null;
                if (carried != null)
                {
                    try
                    {
                        // DropCase recibe explícitamente el destino; no requiere caminar hasta él.
                        _workshop.DropCase(_destinationSlot);
                        _transferStage = StageWaitForDestination;
                        _stageDeadlineFrame = Time.frameCount + 900;
                        Plugin.Log.LogInfo("[RemoteSale] DropCase enviado al mostrador remoto.");
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogError("[RemoteSale] DropCase: " + e);
                        FailTransfer("No se pudo colocar el PC en el mostrador.", true);
                    }
                    return;
                }

                if (Time.frameCount > _stageDeadlineFrame)
                    FailTransfer("El juego no permitió recoger ese PC.", true);
                return;
            }

            if (_transferStage == StageWaitForDestination)
            {
                bool loaded = _destinationSlot != null
                              && SlotHasPc(_destinationSlot)
                              && !_destinationSlot.m_caseLoadingToSlot;
                if (loaded)
                {
                    try
                    {
                        _destinationSlot.SetShopDisplay();
                        var saleInfo = WorkshopUI.s_instance != null
                            ? WorkshopUI.s_instance.m_saleInfo
                            : null;
                        if (saleInfo == null)
                            throw new InvalidOperationException("PCSaleInfo no está disponible.");

                        saleInfo.Activate(_destinationSlot);
                        _transferStage = StageOpenPriceEditor;
                        _stageDeadlineFrame = Time.frameCount + 240;
                        _nextPriceOpenAttempt = Time.frameCount + 3;
                        Plugin.Log.LogInfo("[RemoteSale] PC cargado; abriendo panel de venta remoto.");
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogError("[RemoteSale] Activate: " + e);
                        FailTransfer("El PC llegó, pero no se pudo abrir el panel de venta.", false);
                    }
                    return;
                }

                if (Time.frameCount > _stageDeadlineFrame)
                    FailTransfer("El mostrador tardó demasiado en cargar el PC.", true);
                return;
            }

            if (_transferStage == StageOpenPriceEditor)
            {
                var saleInfo = WorkshopUI.s_instance != null
                    ? WorkshopUI.s_instance.m_saleInfo
                    : null;
                var window = saleInfo != null ? saleInfo.m_setInfoWindow : null;

                if (window != null && window.gameObject.activeInHierarchy
                    && window.m_type == PCSaleInfo.PCSaleInfoType.PRICE)
                {
                    Plugin.Log.LogInfo("[RemoteSale] Editor remoto de precio abierto correctamente.");
                    ShowStatus("PC colocado. Configura el precio y usa PRECIO MÁXIMO.", 5f);
                    ClearTransferState();
                    return;
                }

                if (saleInfo != null && Time.frameCount >= _nextPriceOpenAttempt)
                {
                    try
                    {
                        saleInfo.OnClickEdit(PCSaleInfo.PCSaleInfoType.PRICE);
                        _nextPriceOpenAttempt = Time.frameCount + 15;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning("[RemoteSale] OnClickEdit aún no disponible: " + e.Message);
                    }
                }

                if (Time.frameCount > _stageDeadlineFrame)
                    FailTransfer("El PC quedó en el mostrador, pero no se abrió el editor de precio.", false);
            }
        }

        private void FailTransfer(string message, bool tryRollback)
        {
            if (tryRollback)
                TryRollback();
            Plugin.Log.LogWarning("[RemoteSale] " + message);
            ShowStatus(message, 6f);
            ClearTransferState();
        }

        private void TryRollback()
        {
            try
            {
                if (_workshop != null && _workshop.GetCarriedComputer() != null
                    && _sourceSlot != null && IsSlotEmpty(_sourceSlot))
                {
                    _workshop.DropCase(_sourceSlot);
                    Plugin.Log.LogInfo("[RemoteSale] Rollback: PC devuelto al banco original.");
                    return;
                }

                if (_pendingComputer != null && _sourceSlot != null && IsSlotEmpty(_sourceSlot))
                {
                    if (_destinationSlot != null && SlotHasPc(_destinationSlot))
                        _destinationSlot.DestroyComputer();
                    _sourceSlot.SetComputer(_pendingComputer);
                    Plugin.Log.LogInfo("[RemoteSale] Rollback por ComputerSave completado.");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[RemoteSale] Rollback falló: " + e);
            }
        }

        private void ClearTransferState()
        {
            _transferStage = StageNone;
            _destinationSlot = null;
            _pendingComputer = null;
            _workshop = null;
        }

        private void ClosePicker(bool cancelled)
        {
            if (!_showPicker) return;
            _showPicker = false;
            if (_pickerWorkshop != null)
            {
                _pickerWorkshop.EnableWalking();
                _pickerWorkshop.EnableMouseLook();
                _pickerWorkshop = null;
            }
            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLock;
            if (cancelled) Plugin.Log.LogInfo("[RemoteSale] Selector cancelado.");
        }

        private void DrawStatusMessage()
        {
            if (string.IsNullOrEmpty(_statusMessage) || Time.unscaledTime > _statusUntil) return;
            float width = 520f;
            var rect = new Rect((Screen.width - width) * 0.5f, 30f, width, 42f);
            GUI.Box(rect, _statusMessage);
        }

        private void ShowStatus(string message, float seconds = 4f)
        {
            _statusMessage = message;
            _statusUntil = Time.unscaledTime + seconds;
        }

        private static bool SlotHasPc(BenchSlot slot)
        {
            if (slot == null) return false;
            try { return !slot.IsSlotEmpty() && (slot.m_save != null || slot.caseInSlot != null); }
            catch { return slot.m_save != null || slot.caseInSlot != null; }
        }

        private static bool IsSlotEmpty(BenchSlot slot)
        {
            if (slot == null) return false;
            try { return slot.IsSlotEmpty(); }
            catch { return slot.m_save == null && slot.caseInSlot == null; }
        }

        private static int GetSaleDisplayId(BenchSlot slot)
        {
            if (slot == null) return int.MaxValue - 1;
            return slot.m_saleDisplay != null ? slot.m_saleDisplay.BenchID : slot.GetInstanceID();
        }
    }
}