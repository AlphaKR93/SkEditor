﻿using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit.Editing;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SkEditor.Utilities.Parser;

public partial class CodeSection
{
    public CodeParser Parser { get; private set; }
    public SectionType Type { get; private set; }
    public List<string> Lines { get; set; }
    public int StartingLineIndex { get; private set; }
    public int EndingLineIndex => StartingLineIndex + Lines.Count;

    public HashSet<CodeVariable> Variables { get; private set; } // Case of any section other than options
    public HashSet<CodeOptionReference> OptionReferences { get; private set; } // Case of any section other than options
    public HashSet<CodeOption> Options { get; private set; } // Case of options section

    public HashSet<CodeVariable> UniqueVariables => GetUniqueVariables();
    public HashSet<CodeOptionReference> UniqueOptionReferences => GetUniqueOptionReferences();

    public HashSet<CodeFunctionArgument> FunctionArguments { get; set; } = []; // Case of function section

    public string LinesDisplay => $"From {StartingLineIndex + 1} to {EndingLineIndex}";

    public bool HasAnyVariables => UniqueVariables.Count > 0;
    public bool HasAnyOptionReferences => OptionReferences.Count > 0;
    public bool HasOptionDefinition => Options.Count > 0;
    public bool HasFunctionArguments => FunctionArguments.Count > 0;

    public string Name => GetSectionName();
    public IconSource Icon => Type switch
    {
        SectionType.Command => GetIconFromName("MagicWandIcon"),
        SectionType.Event => GetIconFromName("LightingIcon"),
        SectionType.Function => GetIconFromName("FunctionIcon"),
        SectionType.Options => new SymbolIconSource() { Symbol = Symbol.Setting, FontSize = 20 },
        _ => throw new System.NotImplementedException(),
    };

    public bool ContainsLineIndex(int line) => line >= StartingLineIndex && line <= EndingLineIndex;

    public CodeVariable? GetVariableFromCaret(Caret caret)
        => Variables.FirstOrDefault(v => v.ContainsCaret(caret));

    public CodeOption? GetOptionFromCaret(Caret caret)
        => Options.FirstOrDefault(o => o.ContainsCaret(caret));

    public CodeSection(CodeParser parser, int currentLineIndex, List<string> lines)
    {
        Parser = parser;
        Lines = lines;
        StartingLineIndex = currentLineIndex;
        Parse();
    }

    public void Parse()
    {
        var firstLine = Lines[0];
        Type = firstLine switch
        {
            string s when s.StartsWith("command") || s.StartsWith("discord command") => SectionType.Command,
            string s when s.StartsWith("options:") => SectionType.Options,
            string s when s.StartsWith("function") => SectionType.Function,
            _ => SectionType.Event,
        };

        Options = [];
        Variables = [];
        OptionReferences = [];

        if (Type == SectionType.Options)
        {
            // Parse options
            for (var index = 1; index <= Lines.Count; index++)
            {
                var line = Lines[index - 1];
                if (line.TrimStart(' ', '\t').StartsWith('#'))
                    continue;

                var matches = OptionRegex().Matches(line);
                foreach (var m in matches)
                {
                    var match = m as Match;
                    if (!match.Success)
                        continue;
                    var column = match.Index + 1;
                    var raw = match.Value;
                    Options.Add(new CodeOption(this, raw, StartingLineIndex + index, column));
                }
            }
        }
        else
        {
            int lineIndex = StartingLineIndex;
            foreach (var line in Lines)
            {
                var variableMatches = VariableRegex().Matches(line);
                var optionReferenceMatches = Regex.Matches(line, CodeOptionReference.OptionReferencePattern);

                // Parse variables
                foreach (var m in variableMatches)
                {
                    var match = m as Match;
                    if (!match.Success) continue;
                    var column = match.Index + 1;
                    var raw = match.Value;
                    Variables.Add(new CodeVariable(this, raw, lineIndex + 1, column));
                }

                // Parse option references
                foreach (var m in optionReferenceMatches)
                {
                    var match = m as Match;
                    if (!match.Success) continue;
                    var column = match.Index + 1;
                    var raw = match.Value;
                    OptionReferences.Add(new CodeOptionReference(this, raw, lineIndex + 1, column));
                }

                lineIndex++;
            }

            if (Type == SectionType.Function)
            {
                // Parse function arguments
                var functionArguments = Regex.Matches(Lines[0], CodeFunctionArgument.FunctionArgumentPattern);
                foreach (var m in functionArguments)
                {
                    var match = m as Match;
                    if (!match.Success) continue;
                    var raw = match.Value;
                    var column = match.Index + 1;
                    FunctionArguments.Add(new CodeFunctionArgument(this, raw, StartingLineIndex + 1, column));
                }
            }
        }
    }

    private string GetSectionName()
    {
        string sectionName = Type switch
        {
            SectionType.Command => Lines[0].Split(' ')[1].Split(':')[0].Trim(),
            SectionType.Event => Lines[0].Split(':')[0].Trim(),
            SectionType.Options => "Options",
            SectionType.Function => Lines[0].Split(' ')[1].Split('(')[0].Trim(),
            _ => "Unknown"
        };
        if (sectionName.Length > 20) sectionName = sectionName[..20] + "...";

        return sectionName;
    }

    public enum SectionType
    {
        Command,
        Event,
        Options,
        Function
    }

    /// <summary>
    /// Refresh the editor's content with the new code from this section.
    /// This will replace the previous code that were in the lines of this section.
    /// remove the old code.
    /// </summary>
    public void RefreshCode()
    {
        var lines = Lines;
        var lastLine = lines[^1];
        if (lastLine.EndsWith('\n'))
            lines.RemoveAt(lines.Count - 1);

        var sectionCode = string.Join("\n", lines);
        var editor = Parser.Editor;
        var document = editor.Document;

        var startOffset = document.GetOffset(StartingLineIndex + 1, 0);
        var endOffset = document.GetOffset(EndingLineIndex,
            document.GetLineByNumber(EndingLineIndex).Length + 1);

        document.Replace(startOffset, endOffset - startOffset + (
            EndingLineIndex == document.LineCount ? 0 : 1), sectionCode);
    }

    private static IconSource GetIconFromName(string iconName)
    {
        Application.Current.TryGetResource(iconName, Avalonia.Styling.ThemeVariant.Default, out object icon);
        return icon as IconSource;
    }

    public string VariableTitle => $"Variables ({UniqueVariables.Count})";
    public string OptionReferenceTitle => $"Option References ({UniqueOptionReferences.Count})";
    public string OptionTitle => $"Defined Options ({Options.Count})";
    public string FunctionArgumentTitle => $"Function Arguments ({FunctionArguments.Count})";
    public RelayCommand NavigateToCommand => new(NavigateTo);
    public object[] VariableContent => [new TextBlock() { Text = $"Total: {Variables.Count}" }];

    public CodeFunctionArgument? GetVariableDefinition(CodeVariable variable)
    {
        return FunctionArguments.FirstOrDefault(a => a.IsDefinitionOf(variable));
    }

    public void NavigateTo()
    {
        var editor = Parser.Editor;
        editor.ScrollTo(StartingLineIndex + 1, 0);
        editor.CaretOffset = editor.Document.GetOffset(StartingLineIndex + 1, 0);
        editor.Focus();
    }

    private HashSet<CodeVariable> GetUniqueVariables()
    {
        var uniqueVariables = new HashSet<CodeVariable>();
        foreach (var variable in Variables)
        {
            if (uniqueVariables.Any(v => v.IsSimilar(variable)))
                continue;
            uniqueVariables.Add(variable);
        }
        return uniqueVariables;
    }

    private HashSet<CodeOptionReference> GetUniqueOptionReferences()
    {
        var uniqueOptionReferences = new HashSet<CodeOptionReference>();
        foreach (var optionReference in OptionReferences)
        {
            if (uniqueOptionReferences.Any(o => o.IsSimilar(optionReference)))
                continue;
            uniqueOptionReferences.Add(optionReference);
        }
        return uniqueOptionReferences;
    }

    [GeneratedRegex(@"(.*): (.*)")]
    private static partial Regex OptionRegex();
    [GeneratedRegex(@"(?<=\{)(?!@)_?(.*?)(?=\})")]
    private static partial Regex VariableRegex();
}