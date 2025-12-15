using Amuse.UI.Dialogs;
using Amuse.UI.Models;
using Amuse.UI.Models.FeatureExtractor;
using Microsoft.Extensions.Logging;
using OnnxStack.Core;
using OnnxStack.StableDiffusion.Tokenizers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Amuse.UI.Services
{

    public interface IModeratorService
    {
        bool IsContentFilterEnabled { get; }
        ContentFilterModelSetViewModel ContentFilterModel { get; }
        Task<bool> ContainsExplicitContentAsync(string prompt);
        ClipTokenizer ClipTokenizer { get; }
    }

    public class ModeratorService : IModeratorService
    {
        private readonly ILogger<ModeratorService> _logger;
        private readonly AmuseSettings _settings;
        private readonly IDialogService _dialogService;
        private readonly ClipTokenizer _clipTokenizer;

        private string[] _contentFilterWordList;
        private ContentFilterModelSetViewModel _contentFilterModel;

        public ModeratorService(AmuseSettings settings, IDialogService dialogService, ILogger<ModeratorService> logger)
        {
            _logger = logger;
            _settings = settings;
            _dialogService = dialogService;
            CreateContentFilter();
            _clipTokenizer = CreateCLIPTokenizer();
        }

        public bool IsContentFilterEnabled => _contentFilterModel is not null && !_settings.IsModelEvaluationModeEnabled;
        public ContentFilterModelSetViewModel ContentFilterModel => _contentFilterModel;
        public ClipTokenizer ClipTokenizer => _clipTokenizer;

        public async Task<bool> ContainsExplicitContentAsync(string prompt)
        {
            if (_settings.IsModelEvaluationModeEnabled)
                return false;

            if (_contentFilterWordList.IsNullOrEmpty())
                return false;

            var analyzeResult = AnalyzeExplicitContent(prompt);
            if (analyzeResult.Count == 0)
                return false;

            _logger.LogInformation($"[ModeratorService] [ContainsExplicitContentAsync] - Prompt contains explicit content, Count: {analyzeResult.Count}");
            await _dialogService.ShowMessageDialogAsync
            (
                "Auto Moderator",
                $"Your prompt contains {analyzeResult.Count} words or phrases that might generate explicit or offensive content.\n\nPlease review and edit your prompt as needed.",
                MessageDialog.MessageDialogType.Ok,
                MessageDialog.MessageBoxIconType.Warning,
                MessageDialog.MessageBoxStyleType.Warning
            );
            return true;
        }


        private Dictionary<string, int> AnalyzeExplicitContent(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new Dictionary<string, int>();

            var wordCount = GetWordCount(prompt, _contentFilterWordList);
            return wordCount
                .Where(x => x.Value > 0)
                .ToDictionary();
        }


        private void CreateContentFilter()
        {
            var modelPath = Path.Combine(App.PluginDirectory, "ContentFilter", "ContentFilter.onnx");
            if (File.Exists(modelPath))
            {
                _contentFilterModel = new ContentFilterModelSetViewModel
                {
                    Id = Guid.NewGuid(),
                    ModelSet = new FeatureExtractorModelJson
                    {
                        Name = "ContentFilter",
                        OnnxModelPath = modelPath
                    }
                };
            }

            var wordListPath = Path.Combine(App.PluginDirectory, "ContentFilter", "ContentFilter.bin");
            if (File.Exists(wordListPath))
            {
                _contentFilterWordList = GetWordList(wordListPath);
            }
        }


        private static Dictionary<string, int> GetWordCount(string text, string[] words)
        {
            var counts = new Dictionary<string, int>();
            foreach (string word in words)
            {
                if (Regex.Match(text, @$"\b{word}\b", RegexOptions.IgnoreCase).Success)
                {
                    if (!counts.ContainsKey(word))
                        counts.Add(word, 0);

                    counts[word]++;
                }
            }
            return counts;
        }


        private static string[] GetWordList(string filePath)
        {
            var words = new List<string>();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            {
                var buffer = new byte[4];
                while (fileStream.Read(buffer) == buffer.Length)
                {
                    int length = BitConverter.ToInt32(buffer, 0);
                    var wordBytes = new byte[length];
                    fileStream.ReadExactly(wordBytes, 0, length);
                    for (int i = 0; i < wordBytes.Length; i++)
                    {
                        wordBytes[i] = (byte)((wordBytes[i] >> 2) | (wordBytes[i] << 6)); // Right shift by 2 bits
                    }
                    words.Add(Encoding.UTF8.GetString(wordBytes));
                }
                return words.ToArray();
            }
        }


        private static ClipTokenizer CreateCLIPTokenizer()
        {
            var config = new TokenizerConfig
            {
                OnnxModelPath = Path.Combine(App.PluginDirectory, "CLIPTokenizer", "vocab.json")
            };
            return new ClipTokenizer(config);
        }
    }
}
