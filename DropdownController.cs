using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

namespace UnityVolumeRendering
{
    public class DropdownController : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown modeDropdown;
        public VolumeRenderedObject volumeRenderedObject;
        
        
        // Start is called before the first frame update
        void Start()
        {
            modeDropdown = GetComponent<TMP_Dropdown>();
            modeDropdown.ClearOptions();

            List<string> modeList = new List<string>();
            
            // Add RenderMode values to modeList
            foreach(RenderMode mode in System.Enum.GetValues(typeof(RenderMode)))
            {
                modeList.Add(mode.ToString());
            }
            // Put the option values on modeList to modeDropdown
            modeDropdown.AddOptions(modeList);

            // Add listener to dropdown change event
            modeDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            
        }
        // Method called when dropdown value changes
        void OnDropdownValueChanged(int index)
        {
            // Get the selected rendermode
            RenderMode selectedMode = (RenderMode)index;
            // Update the rendermode in VolumeRenderedObject
            volumeRenderedObject.SetRenderMode(selectedMode);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}

