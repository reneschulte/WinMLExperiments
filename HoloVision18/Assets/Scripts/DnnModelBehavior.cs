using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DnnModelBehavior : MonoBehaviour
{
    private SqueezeNetModel _dnnModel;

    public TextMesh StatusBlock;

    async void Start()
    {
        _dnnModel = new SqueezeNetModel();
        StatusBlock.text = $"Loading {SqueezeNetModel.ModelFileName} ... patience ";

        try
        {
            await _dnnModel.LoadModelAsync(false);
            StatusBlock.text = $"Loaded model.";
        }
        catch (Exception ex)
        {
            StatusBlock.text = $"Failed loading model: {ex.Message}";
            Debug.LogError(ex);
        }
    }
}
