using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;
using KModkit;
using UnityEngine.Serialization;
using Random = System.Random;


public enum MorseType
{
	Dot,
	Dash
}

public enum FanState
{
	Static,
	Clock,
	Counter,
}

public static class NotTheFanExtensions
{
	public static readonly Dictionary<char, string> MorseLetters = new Dictionary<char, string>
	{
		{ '0', "-----" }, { '1', ".----" }, { '2', "..---" }, { '3', "...--" }, { '4', "....-" }, { '5', "....." },
		{ '6', "-...." }, { '7', "--..." }, { '8', "---.." }, { '9', "----." }, { 'A', ".-" }, { 'B', "-..." }, 
		{ 'C', "-.-." }, { 'D', "-.." }, { 'E', "." }, { 'F', "..-." }, { 'G', "--." }, 
		{ 'H', "...." }, { 'I', ".." }, { 'J', "---." }, { 'K', "-.-" }, { 'L', ".-.." }, 
		{ 'M', "--" }, { 'N', "-." }, { 'O', "---" }, { 'P', ".--." }, { 'Q', "--.-" }, 
		{ 'R', ".-." }, { 'S', "..." }, { 'T', "-" }, { 'U', "..-" }, { 'V', "...-" }, 
		{ 'W', ".--" }, { 'X', "-..-" }, { 'Y', "-.--" }, { 'Z', "..--" }
	};
    
	public static string ToMorse(this string input) 
	{ return input.Aggregate("", (current, c) => current + MorseLetters[char.ToUpper(c)]); }
}

public class NotTheFanScript : MonoBehaviour
{
	public KMBombInfo Bomb;
	public KMBombModule Module;
	public KMAudio Audio;
	public ParticleSystem MistParticles;
	public GameObject SmokeParticles;
	public MeshRenderer powerLightMeshRenderer;
	public Material powerLightOnMat;
	public Material powerLightOffMat;
	
	private Random _random = new Random();

	private string[] _solutionWords;

	private static int _moduleIdCounter = 1;
	private int moduleId;
	private bool _isSolved;
	
	private static readonly Random Random = new Random();

	public GameObject fanBlades;
	private float _fanSpeed;
	private float _fanTargetSpeed;
	private int _direction;
	private bool _isOn;
	private bool _isDeactivated;
	public TextMesh displayText; 

	public KMSelectable directionButton;
	public KMSelectable powerButton;

	public float rotationSpeed = 10f;

	private string _fullUserInput = "";
	private string _userInput = "";
	private string _expectedInput = "";
	private int _completedLetterCount = 0;
	private int _completedWordCount = 0;
	private FanState[] _currentStateSet;
	public MeshRenderer[] StageLEDs;
	
	void Awake()
	{
		moduleId = _moduleIdCounter++;
		_direction = 1;
		_isOn = true;
		powerLightMeshRenderer.material = powerLightOnMat;

		var didStrike = false;
		powerButton.OnInteract += () => RegisterInput(MorseType.Dot, out didStrike);
		directionButton.OnInteract += () => RegisterInput(MorseType.Dash, out didStrike);
	}
	
	void Start ()
	{
		_fanSpeed = 0;
		StartCoroutine(Spin());
		ResetModule();
	}
	
	void ResetModule()
	{
		_userInput = "";
		_fullUserInput = "";
		_solutionWords = GetSolution();
		displayText.text = _solutionWords[0];
		rotationSpeed = 10;
		Debug.Log("[Not The Fan #" + moduleId + "] New words generated!");
		Debug.Log("[Not The Fan #" + moduleId + "] Step 1 initial word: " + _solutionWords[0].ToUpperInvariant());
		Debug.Log("[Not The Fan #" + moduleId + "] Step 2 words: " + _solutionWords.Skip(1).Take(4).Join().ToUpperInvariant());
		Debug.Log("[Not The Fan #" + moduleId + "] Step 3 final word: " + _solutionWords.Last().ToUpperInvariant());
		_completedLetterCount = 0;
		_completedWordCount = 0;
		foreach (var led in StageLEDs)
			led.material = powerLightOffMat;
		SetNextExpectedInput();
	}
	
	void SetNextExpectedInput()
	{
		var word = _solutionWords[_completedWordCount];
		if (_completedWordCount < 5)
		{
			var nextWord = _solutionWords[_completedWordCount + 1];
			_currentStateSet = GetFanStates(nextWord);
		}
		var c = word[_completedLetterCount];
		_expectedInput = c.ToString().ToMorse();
	}

	bool RegisterInput(MorseType type, out bool didStrike)
	{
		didStrike = false;
		displayText.text = "";
		directionButton.AddInteractionPunch();
		Audio.PlaySoundAtTransform(type == MorseType.Dash ? "morse-dash" : "morse-dot", transform);
		var piece = type == MorseType.Dash ? "-" : ".";
		_userInput += piece;
		_fullUserInput += piece;

		if (CheckNewLetter()) 
			return false;
		didStrike = true;
		Module.HandleStrike();
		var onWord = _solutionWords[_completedWordCount].ToUpperInvariant();
		var onLetter = char.ToUpperInvariant(onWord[_completedLetterCount]);
		Debug.Log("[Not The Fan #" + moduleId + "] Strike: Incorrect input: " + piece + " at word: " + onWord + " on letter: " + onLetter);
		Debug.Log("[Not The Fan #" + moduleId + "] Input up to strike: " + _fullUserInput);
		ResetModule();
		return false;
	}

	//"[NOT THE FAN #" + moduleId + "] "
	
	public string[] GetSolution()
	{
		var keywordRows = _wordList
			.Select((_, index) => index / 4).Distinct().OrderBy(_ => _random.Next()).Take(5).ToArray();
		var keywords = keywordRows
			.Select(row => _wordList[row * 4 + _random.Next(0, 4)]).ToList();
		var keywordDigitSum = keywords
			.SelectMany(word => word.Where(char.IsDigit)).Select(digitChar => digitChar - '0').Sum(digit => (digit + 1) * 3);
		var keywordRow = keywordDigitSum % 26;
		var finalWord = _wordList[keywordRow * 4];
		keywords.Add(finalWord);
		return keywords.ToArray();
	}

	private FanState[] GetFanStates(string word)
	{
		var wordIndex = Array.IndexOf(_wordList, word);
		var letterIndex = wordIndex / 4 + 1;
		var state1 = letterIndex / 9;
		var state2 = (letterIndex - state1 * 9) / 3;
		var state3 = letterIndex - state1 * 9 - state2 * 3;
		var configIndex = (wordIndex % 4);
		var state4 = configIndex / 2;
		var state5 = configIndex - state4 * 2;
		return new[] { (FanState)state1, (FanState)state2, (FanState)state3, 
			state4 == 0 ? FanState.Counter : FanState.Clock, state5 == 0 ? FanState.Counter : FanState.Clock };
	}
	
	private bool CheckNewLetter()
	{
		if (!_expectedInput.StartsWith(_userInput))
			return false;

		if (_expectedInput != _userInput)
			return true;

		_userInput = "";
		if (_completedWordCount < 5)
			SetFanState(_currentStateSet[_completedLetterCount]);
		else
		{
			_direction = 1;
			rotationSpeed *= 1.5f;
		}
		_completedLetterCount++;
		_fullUserInput += " ";
		if (_completedLetterCount == 5)
		{
			_fullUserInput += "      ";
			HandleCompletedWord();
			return true;
		}
		
		SetNextExpectedInput();
		return true;
	}

	private void SetFanState(FanState currentState)
	{
		_isOn = currentState != FanState.Static;
		_direction = currentState == FanState.Clock ? 1 : -1;
	}

	private void HandleCompletedWord()
	{
		_completedWordCount++;
		if (_completedWordCount == 6)
		{
			Module.HandlePass();
			_isSolved = true;
			_isDeactivated = true;
			_isOn = false;
			_fanTargetSpeed = 0;
			_fanSpeed = Mathf.Min(_fanSpeed, 50);
			Audio.PlaySoundAtTransform("Bang", fanBlades.transform);
			SmokeParticles.gameObject.SetActive(true);
			Module.HandlePass();
			return;
		}

		StageLEDs[_completedWordCount - 1].material = powerLightOnMat;
		_completedLetterCount = 0;
		SetNextExpectedInput();
	}
	
	private float _lastSoundTime = -1f; 
	IEnumerator Spin()
	{
		while (true)
		{
			if (Math.Abs(_fanSpeed) < 2)
				MistParticles.gameObject.SetActive(false);
			
			_fanTargetSpeed = rotationSpeed * _direction * (_isOn ? 1 : 0);
			powerLightMeshRenderer.material = _isOn ? powerLightOnMat : powerLightOffMat;
			
			MistParticles.gameObject.SetActive(_fanTargetSpeed != 0);
			_fanSpeed = Mathf.Lerp(_fanSpeed, _fanTargetSpeed, Time.deltaTime);
			fanBlades.transform.Rotate(Vector3.up, _fanSpeed);
			
			var main = MistParticles.main;
			main.startSpeed = Mathf.Clamp(Math.Abs(_fanSpeed), 0.2f, 10f);
			main.startLifetime = Mathf.Clamp(1.5f / Mathf.Log(1 + Math.Abs(_fanSpeed)), 0.5f, 1.5f);
			
			if (Math.Abs(_fanSpeed) > 0.1f) 
			{
				var absFanSpeed = Mathf.Abs(_fanSpeed);
				var tickInterval = 3f / Mathf.Clamp(absFanSpeed, 0.01f, 30f);
				if (Time.time - _lastSoundTime >= tickInterval)
				{
					Audio.PlaySoundAtTransform("FanTick" + Random.Next(1, 3), fanBlades.transform);
					_lastSoundTime = Time.time;
				}
			}
			else
				_fanSpeed = 0;

			yield return null;
		}
	} 
	
	private readonly string[] _wordList =
	{
		"AnGle", "AnGl3", "AnG1e", "AnG13", 
		"blAde", "blAd3", "8lAde", "8lAd3",
		"clocK", "cl0cK", "c1ocK", "c10cK",
		"dRAfT", "dRAf7", "dR4fT", "dR4f7",
		"edGes", "edGe5", "edG3s", "edG35",
		"flows", "fl0ws", "f1ows", "f10ws",
		"GRIll", "GRI1l", "GR1ll", "GR11l",
		"HoVeR", "HoV3R", "H0VeR", "H0V3R",
		"InPUT", "InPU7", "1nPUT", "1nPU7",
		"joInT", "jo1nT", "7oInT", "7o1nT",
		"Knobs", "Knob5", "Kn0bs", "Kn0b5",
		"lARGe", "lARG3", "l4RGe", "l4RG3",
		"moToR", "mo7oR", "m0ToR", "m07oR",
		"noIse", "noIs3", "noI5e", "noI53",
		"oRbIT", "oRbI7", "oR6IT", "oR6I7",
		"PoweR", "Pow3R", "P0weR", "P0w3R",
		"qUIcK", "qU1cK", "9UIcK", "9U1cK",
		"RoboT", "Ro6oT", "Robo7", "Ro6o7",
		"sPIns", "sP1ns", "5PIns", "5P1ns",
		"TwIsT", "TwI5T", "TwIs7", "TwI57",
		"UPdoG", "UPdo9", "UP4oG", "UP4o9",
		"VAlUe", "VAlU3", "V4lUe", "V4lU3",
		"woosH", "wo0sH", "w0osH", "w00sH",
		"XeRic", "XeR1c", "X3Ric", "X3R1c",
		"yIeld", "yI3ld", "y1eld", "y13ld",
		"zooms", "zoom5", "2ooms", "2oom5",
		// First three fans give the row
		// Last two fans give the column
	};
	
	
#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"!{0} input <morse> [. or -], !{0} i <morse>, !{0} in <morse>, EXAMPLE: !1 input ..-..--.... (fans)";
#pragma warning restore 414

	IEnumerator ProcessTwitchCommand(string command)
	{
		command = command.ToLowerInvariant();
		var match = Regex.Match(command, @"^\s*(?:i|in|input)\s+([.-]+)", RegexOptions.IgnoreCase);
		if (!match.Success)
			yield break;

		string morse = match.Groups[1].Value;
		yield return StartCoroutine(PlayMorseQueue(morse));
		yield return null;
	}
	
	IEnumerator PlayMorseQueue(string morse)
	{
		foreach (var symbol in morse)
		{
			bool didStrike;
			switch (symbol)
			{
				case '.':
					RegisterInput(MorseType.Dot, out didStrike);
					if (didStrike)
						yield break;
					yield return new WaitForSeconds(0.1f);
					break;

				case '-':
					RegisterInput(MorseType.Dash, out didStrike);
					if (didStrike)
						yield break;
					yield return new WaitForSeconds(0.2f);
					break;
			}
		
			yield return new WaitForSeconds(0.025f);
		}
	}
}
