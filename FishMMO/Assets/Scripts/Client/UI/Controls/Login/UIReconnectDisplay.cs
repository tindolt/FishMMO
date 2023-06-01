using FishMMO.Client;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIReconnectDisplay : UIControl
{
	[Header("Reconnect Screen Parameters")]
	public Button CancelButton;
	public TMP_Text CancelButtonText;
	public TMP_Text AttemptCounterText;

	public override void OnStarting()
	{
		Client.OnReconnectAttempt += OnReconnectAttemptsChanged;
		Client.OnConnectionSuccessful += OnCloseScreen;
		Client.OnReconnectFailed += OnCloseScreen;
	}

	public override void OnDestroying()
	{
		Client.OnReconnectAttempt -= OnReconnectAttemptsChanged;
		Client.OnConnectionSuccessful -= OnCloseScreen;
		Client.OnReconnectFailed -= OnCloseScreen;
	}

	public void OnReconnectAttemptsChanged(byte attempts, byte maxAttempts)
	{
		AttemptCounterText.text = $"Attempt {attempts} of {maxAttempts}";
		visible = true;
	}

	public void OnCancelClicked()
	{
		Client.ReconnectCancel();
		visible = false;
	}

	public void OnCloseScreen()
	{
		visible = false;
	}
}
