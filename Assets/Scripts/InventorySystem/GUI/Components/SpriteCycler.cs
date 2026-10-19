using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem.GUI.Components
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
        
        private void Awake() => _frames = Resources.LoadAll<Sprite>(spriteSheet.name);

        private void Update()
        {
            if (Image.enabled == _enabled) 
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
                _image.sprite = _frames[_index];
                
                _index = (_index + 1) % _frames.Length;

                yield return new WaitForSeconds(delaySeconds);
            }
        }
    }
}