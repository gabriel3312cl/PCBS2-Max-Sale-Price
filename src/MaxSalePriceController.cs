using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PCBS2MaxSalePrice
{
    public sealed class MaxSalePriceController : MonoBehaviour
    {
        private const string ButtonObjectName = "PCBS2_MaxSalePrice_Button";

        private SetPCInfo _window;
        private GameObject _buttonObject;
        private Button _button;
        private int _nextScanFrame;

        public MaxSalePriceController(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            try
            {
                if (Time.frameCount < _nextScanFrame) return;
                _nextScanFrame = Time.frameCount + 10;

                var saleInfo = WorkshopUI.s_instance != null
                    ? WorkshopUI.s_instance.m_saleInfo
                    : null;
                var window = saleInfo != null ? saleInfo.m_setInfoWindow : null;

                if (window == null)
                {
                    HideButton();
                    return;
                }

                if (_window != window || _buttonObject == null)
                {
                    _window = window;
                    CreateButton(window);
                }

                bool editingPrice =
                    window.gameObject.activeInHierarchy
                    && window.m_type == PCSaleInfo.PCSaleInfoType.PRICE;

                if (_buttonObject != null && _buttonObject.activeSelf != editingPrice)
                    _buttonObject.SetActive(editingPrice);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[MaxSalePrice] Update: " + e);
            }
        }

        private void CreateButton(SetPCInfo window)
        {
            HideButton();

            var source = window.m_saveButton;
            if (source == null || source.transform.parent == null)
            {
                Plugin.Log.LogWarning("[MaxSalePrice] El botón Guardar aún no está disponible.");
                return;
            }

            // Reutiliza exactamente el estilo visual del botón Guardar.
            _buttonObject = Instantiate(source.gameObject, source.transform.parent);
            _buttonObject.name = ButtonObjectName;
            _buttonObject.hideFlags = HideFlags.DontSave;

            _button = _buttonObject.GetComponent<Button>();
            if (_button == null)
            {
                Plugin.Log.LogError("[MaxSalePrice] No se pudo clonar el componente Button.");
                Destroy(_buttonObject);
                _buttonObject = null;
                return;
            }

            _button.onClick.RemoveAllListeners();
            var clickAction = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(
                new Action(OnMaxPriceClicked));
            _button.onClick.AddListener(clickAction);

            var label = _buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = "PRECIO MÁXIMO";

            PositionButton(source, _button);
            _buttonObject.SetActive(false);
            Plugin.Log.LogInfo("[MaxSalePrice] Botón creado en el panel de precio.");
        }

        private static void PositionButton(Button source, Button created)
        {
            var sourceRect = source.GetComponent<RectTransform>();
            var rect = created.GetComponent<RectTransform>();
            if (sourceRect == null || rect == null) return;

            var parent = source.transform.parent;
            var layout = parent.GetComponent<LayoutGroup>();
            if (layout != null)
            {
                // En diseños automáticos, insertar el botón antes de Guardar.
                created.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
                return;
            }

            // En el diseño absoluto de PCBS2, colocarlo sobre Guardar.
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.sizeDelta = new Vector2(
                Mathf.Max(sourceRect.sizeDelta.x, 210f),
                sourceRect.sizeDelta.y);
            rect.anchoredPosition = sourceRect.anchoredPosition
                                    + new Vector2(0f, sourceRect.rect.height + 12f);
        }

        private void OnMaxPriceClicked()
        {
            try
            {
                if (_window == null
                    || _window.m_type != PCSaleInfo.PCSaleInfoType.PRICE
                    || _window.m_inputFieldPrice == null)
                    return;

                int maxPrice = _window.m_maxPrice;
                if (maxPrice < 0) maxPrice = 0;

                string value = maxPrice.ToString(CultureInfo.InvariantCulture);
                _window.m_inputFieldPrice.text = value;
                _window.CheckPrice();

                Plugin.Log.LogInfo($"[MaxSalePrice] Precio máximo aplicado: {value}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[MaxSalePrice] Click: " + e);
            }
        }

        private void HideButton()
        {
            if (_buttonObject != null)
                _buttonObject.SetActive(false);
        }
    }
}
