using System.Collections;
using UnityEngine;

/// <summary>
/// Interface cho các hiệu ứng có thể được quản lý bởi EffectManager
/// </summary>
public interface IEffect
{
    /// <summary>
    /// Tên của hiệu ứng
    /// </summary>
    string EffectName { get; }
    
    /// <summary>
    /// Thời gian thực hiện hiệu ứng (giây)
    /// </summary>
    float Duration { get; set; }
    
    /// <summary>
    /// Trạng thái hiệu ứng có đang chạy hay không
    /// </summary>
    bool IsPlaying { get; }
    
    /// <summary>
    /// GameObject mà hiệu ứng được áp dụng
    /// </summary>
    GameObject Target { get; set; }
    
    /// <summary>
    /// Bắt đầu hiệu ứng
    /// </summary>
    /// <param name="onComplete">Callback khi hiệu ứng hoàn thành</param>
    void PlayEffect(System.Action onComplete = null);
    
    /// <summary>
    /// Dừng hiệu ứng
    /// </summary>
    void StopEffect();
    
    /// <summary>
    /// Reset hiệu ứng về trạng thái ban đầu
    /// </summary>
    void ResetEffect();
    
    /// <summary>
    /// Thiết lập các tham số cho hiệu ứng
    /// </summary>
    /// <param name="parameters">Dictionary chứa các tham số</param>
    void SetParameters(System.Collections.Generic.Dictionary<string, object> parameters);
}
