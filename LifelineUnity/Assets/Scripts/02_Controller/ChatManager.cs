﻿using System;
using System.IO;
using SimpleJSON;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class ChatManager : MonoBehaviour
{
    //==============================================================================================
    // Fields
    public Queue<string> m_leftChats = new Queue<string>();
    private JSONNode status = new JSONClass();
    private JSONNode scenes = new JSONClass();
    private JSONNode choices = new JSONClass();
    private string status_savePath;
    private float m_timer = 0.0f;
    private bool m_isGameOver = false;
    private View m_view;
    private bool m_if_else = false;
    private bool m_skip_line = false;

    //==============================================================================================
    // Methods

    void Awake()
    {
        Application.targetFrameRate = 10;
        m_view = FindObjectOfType(typeof(View)) as View;
    }
    void Start()
    {
        m_view.m_startGameButton.onClick.AddListener(() => PlayGame());
        m_view.m_rePlayGameButton.onClick.AddListener(() => RePlayGame());
        status_savePath = Application.persistentDataPath + "/status0.json";
        LoadStoryData();
    }

    void Update()
    {
        if (status["atScene"] != null)
        {
            AtScene(status["atScene"]);     // set: status["atScene"] = null
        }
        else
        {
            if (m_isGameOver)
            {
                m_isGameOver = false;
                StartCoroutine(DisplayRePlayButton(0.3f));
            }
        }
        PopLeftChat();
    }

    public void PlayGame()
    {
        LoadStatusData();
        m_view.m_startGameButton.gameObject.SetActive(false);
        m_view.m_textBoxPanle.SetActive(true);
    }
    public void RePlayGame()
    {
        m_isGameOver = false;
        m_view.m_rePlayGameButton.gameObject.SetActive(false);
        m_view.Initialize();
        StartCoroutine(RePlayGame(0.5f));
    }

    IEnumerator DisplayRePlayButton(float duration)
    {
        yield return new WaitForSeconds(duration);
        m_view.m_rePlayGameButton.gameObject.SetActive(true);
    }
    IEnumerator RePlayGame(float duration)
    {
        yield return new WaitForSeconds(duration);
        SaveStatusData("Start");
    }
    void LoadStoryData()
    {
        // 以下方式通过 Adroid, Mac 测试, iOS不通过
        // string choices_file_path = Resources.Load("Data/choices_cn").ToString();
        // string scenes_file_path = Resources.Load("Data/waypoints_cn").ToString();
        // 以下方式通过 Adroid, Mac，iOS测试
        TextAsset choices_file = Resources.Load("Data/choices_cn") as TextAsset;
        TextAsset scenes_file = Resources.Load("Data/scenes_cn") as TextAsset;
        // Debug.Log("scenes_file = " + scenes_file);

        // 将文本解析成 JSONNode
        choices = JSONNode.Parse(choices_file.text);
        scenes = JSONNode.Parse(scenes_file.text);
    }
    void LoadStatusData()
    {
        if (File.Exists(status_savePath))
        {
            status = JSONNode.LoadFromFile(status_savePath);
        }
        else
        {
            status["atScene"] = "Start";
        }
    }

    void SaveStatusData(string scene)
    {
        status["atScene"] = scene;
        status.SaveToFile(status_savePath);
    }

    void AtScene(string scene)
    {
        // Debug.Log("AtScene: scenes[scene] = " + scenes[scene].ToString());
        status["atScene"] = null;
        JSONArray sceneItem = scenes[scene].AsArray;
        for (int i = 0; i < sceneItem.Count; i++)
        {
            // 转换成 string, 去掉首尾的 “
            string line = sceneItem[i].ToString().Substring(1, sceneItem[i].ToString().Length - 2);
            // Debug.Log("line = " + line);
            if (m_if_else)
            {
                if (line.StartsWith("<<else"))
                {
                    m_skip_line = !m_skip_line;
                    continue;
                }
                else if (line.Equals("<<endif>>"))
                {
                    m_if_else = false;
                    continue;
                }
                if (m_skip_line) continue;
            }
            if (line.StartsWith("<<if") || line.StartsWith("<<elseif"))
            {
                m_if_else = true;
                string LineWithNoTag = line.Substring(2, line.Length - 4);
                if (LineWithNoTag.Contains(" and "))
                {
                    m_skip_line = and_InLine(LineWithNoTag);
                }
                else if (LineWithNoTag.Contains(" or "))
                {
                    m_skip_line = or_InLine(LineWithNoTag);
                }
                else
                {
                    m_skip_line = and_or_NotInLine(LineWithNoTag);
                }
            }
            else if (line.StartsWith("<<set")) HandleSet(line);
            else if (line.StartsWith("[[")) ToNewScene(line);
            else if (line.StartsWith("<<category")) HandleChoice(line, scene);
            else LeftChat(line);
        }
    }

    bool and_InLine(string line)
    {
        string[] newline = line.Split(' ');
        // "gte" 和 “and” 同时出现的行
        if (line.Contains(" gte "))
        {
            int valueA = status[newline[1].Substring(1)].AsInt;
            string valueB = status[newline[5].Substring(1)];
            bool isgte = valueA >= int.Parse(newline[3]);
            bool isTrueOrFalse = valueB.Equals(newline[7]);

            // Debug.Log("valueOne = " + valueOne + " valueTwo = " + valueTwo);
            // Debug.Log("gteline[3]= " + gteline[3] + " gteline[7] = " + gteline[7]);
            return !(isgte && isTrueOrFalse);
            // Debug.Log("and gte skip_line  = " + skip_line);
        }
        else
        {
            string valueA = status[newline[1].Substring(1)];
            string valueB = status[newline[5].Substring(1)];
            // Debug.Log("valueA = " + valueA + " valueB = " + valueB);
            // Debug.Log("newline[3] = " + newline[3] + " newline[7] = " + newline[7]);
            return  !(valueA.Equals(newline[3]) && valueB.Equals(newline[7]));
            // Debug.Log("and skip_line = " + skip_line);
        }
    }

    bool or_InLine(string line)
    {
        string[] newline = line.Split(' ');
        string valueA = status[newline[1].Substring(1)];
        string valueB = status[newline[5].Substring(1)];

        // Debug.Log("valueA = " + valueA + " valueB = " + valueB);
        // Debug.Log("newline[3] = " + newline[3] + " newline[7] = " + newline[7]);
        return !(valueA.Equals(newline[3]) || valueB.Equals(newline[7]));
        // Debug.Log("and skip_line = " + skip_line);
    }
    bool and_or_NotInLine(string line)
    {
        string[] newline = line.Split(' ');
        if (line.Contains(" gte "))
        {
            int valueA = status[newline[1].Substring(1)].AsInt;
            return !(valueA >= int.Parse(newline[3]));
            // Debug.Log("skip_line = " + skip_line);
        }
        else
        {
            // Debug.Log("variable = " + status[newline[1].Substring(1)] + " + " + newline[3]);
            // 下面两句不能写成skip_line = !(status[newline[1].Substring(1)].Equals(newline[3]))，会得到错误结果 </summary>
            string value = status[newline[1].Substring(1)];
            return !value.Equals(newline[3]);
            // Debug.Log("skip_line = " + skip_line);
        }
    }
    void HandleSet(string line)
    {
        string[] lines = line.Substring(7, line.Length - 9).Replace(" ", "").Split('=');
        if (lines[1].Contains("-1"))
        {
            int value = status[lines[0]].AsInt - 1;
            status[lines[0]] = value.ToString();
        }
        else status[lines[0]] = lines[1];
    }
    void ToNewScene(string line)
    {
        string newLine = line.Substring(2, line.Length - 4);
        if (newLine.StartsWith("delay"))
        {
            string[] newLines = newLine.Split('|');
            status["atScene"] = newLines[1];
        }
        else status["atScene"] = newLine;
    }

    void HandleChoice(string line, string scene)
    {
        JSONArray choice = choices[int.Parse(line.Substring(19, line.Length - 21))]["actions"].AsArray;
        Chat.Choose(m_view, new Dictionary<string, Action<string>> {
            // choiceButtonOne
            {choice[0]["choice"], message => {
                ActionFunction(choice, message, 0);
            }},
            // choiceButtonTwo
            {choice[1]["choice"], message => {
                ActionFunction(choice, message, 1);
            }}
         });
    }

    void ActionFunction(JSONArray choice, string message, int index)
    {
        string newScence = choice[index]["identifier"];
        Chat.RightSay(m_view, message);
        status["atScene"] = newScence;
        SaveStatusData(newScence);
        // SaveStatusData(scene);
    }

    void LeftChat(string line)
    {
        if (line.Contains("$pills") || line.Contains("$glowrods") || line.Contains("$power"))
        {
            // 替换$pills、$glowrods 和 $power
            string newLine = line.Replace("$pills", status["pills"]).Replace("$glowrods", status["glowrods"]).Replace("$power", status["power"]);
            Chat.LeftSay(this, newLine);
        }
        else Chat.LeftSay(this, line);
    }

    /// <summary> 弹出左侧对话 </summary>
    private void PopLeftChat()
    {
        // 左侧有对话
        if (m_leftChats.Count > 0)
        {
            // 隐藏选择面板
            m_view.HideChoicePanel();
            m_timer += Time.deltaTime;
            // 若左侧对话不为空，且计时器时间到，继续下一句.
            while (m_leftChats.Count > 0 && m_timer >= 1.5f)
            {
                string leftChat = m_leftChats.Peek();
                if (leftChat.Equals("游戏结束")) m_isGameOver = true;
                m_view.PopBubble(leftChat, m_view.m_SoundManager.m_leftAudio);
                m_leftChats.Dequeue();
                m_timer = 0f;
            }
        }
        // 左侧没有对话
        else
        {
            if (m_view.m_HasRightChat)
                m_view.HideChoicePanel();     // 右侧没有对话
            else
                m_view.ShowChoicePanel();     // 右侧有对话
        }
    }
}
