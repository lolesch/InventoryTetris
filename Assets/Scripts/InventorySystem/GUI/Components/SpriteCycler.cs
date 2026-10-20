using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components
{
    [RequireComponent(typeof(Image))]
    public sealed class SpriteCycler : MonoBehaviour
    {
        [Tooltip("The sprite sheet needs to be in a 'Resources' folder.")]
        [SerializeField] private Texture2D spriteSheet;
        [SerializeField, Range(0.05f,.5f)] private float delaySeconds = 0.1f;

        private Image _image;
        private Image Image => _image ? _image : _image = GetComponent<Image>();

        private Sprite[] _frames;
        private int _index = 0;
        private bool _enabled;
        
        private void Awake()
        {
            if (spriteSheet == null)
            {
                Debug.LogWarning($"{name}: no sprite sheet assigned, so nothing cycles.", this);
                return;
            }

            _frames = Resources.LoadAll<Sprite>(spriteSheet.name);
            if (_frames.Length == 0)
                Debug.LogWarning($"{name}: '{spriteSheet.name}' has no sprites in a Resources folder, so nothing cycles.", this);
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0 || Image.enabled == _enabled)
                return;
            
            _enabled = Image.enabled;
            if (_enabled)
                StartCoroutine(CycleSprites());
            else
                StopAllCoroutines();
        }

        private IEnumerator CycleSprites()
        {
            while (Image.enabled)
            {
                Image.sprite = _frames[_index];
                
                _index = (_index + 1) % _frames.Length;

                yield return new WaitForSeconds(delaySeconds);
            }
        }
    }
}