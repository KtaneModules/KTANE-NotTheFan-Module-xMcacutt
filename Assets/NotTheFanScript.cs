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
		Debug.Log("[Not The Fan #" + moduleId + "] Step 2 words: " + _solutionWords.Skip(1).Take(2).Join().ToUpperInvariant());
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
		if (_completedWordCount < 3)
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
			.Select((_, index) => index / 4).Distinct().OrderBy(_ => _random.Next()).Take(3).ToArray();
		var keywords = keywordRows
			.Select(row => _wordList[row * 4 + _random.Next(0, 4)]).ToList();
		var keywordDigitSum = keywords
			.SelectMany(word => word.Where(char.IsDigit))
			.Select(digitChar => digitChar - '0')
			.Sum(digit => (digit + 1) * 3);
		var keywordValue = ((keywordDigitSum - 1) % 26 + 1);
		var keywordRow = keywordValue - 1; 
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
		var state4 = configIndex % 2;
		var state5 = configIndex / 2;
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
		if (_completedWordCount < 3)
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
		if (_completedWordCount == 4)
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
		"AnGle", "AnG1e", "AnGl3", "AnG13", 
		"blAde", "6lAde", "blAd3", "6lAd3",
		"clocK", "c1ocK", "cl0cK", "c10cK",
		"dRAfT", "dR4fT", "dRAf7", "dR4f7",
		"edGes", "edG3s", "edGe5", "edG35",
		"flows", "f1ows", "fl0ws", "f10ws",
		"GRIll", "GR1ll", "GRI1l", "GR11l",
		"HoVeR", "H0VeR", "HoV3R", "H0V3R",
		"InPUT", "1nPUT", "InPU7", "1nPU7",
		"joInT", "7oInT", "jo1nT", "7o1nT",
		"Knobs", "Kn0bs", "Knob5", "Kn0b5",
		"lARGe", "l4RGe", "lARG3", "l4RG3",
		"moToR", "m0ToR", "mo7oR", "m07oR",
		"noIse", "noI5e", "noIs3", "noI53",
		"oRbIT", "oR6IT", "oRbI7", "oR6I7",
		"PoweR", "P0weR", "Pow3R", "P0w3R",
		"qUIcK", "9UIcK", "qU1cK", "9U1cK",
		"RoboT", "Robo7", "8oboT", "8obo7",
		"sPIns", "5PIns", "sP1ns", "5P1ns",
		"TwIsT", "TwIs7", "TwI5T", "TwI57",
		"UPdoG", "UP4oG", "UPdo9", "UP4o9",
		"VAlUe", "V4lUe", "VAlU3", "V4lU3",
		"woosH", "w0osH", "wo0sH", "w00sH",
		"XeRIc", "X3RIc", "XeR1c", "X3R1c",
		"yIeld", "y1eld", "yI3ld", "y13ld",
		"zooms", "2ooms", "zoom5", "2oom5",
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
