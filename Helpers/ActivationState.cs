using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace FirebirdWeb.Helpers
{
    // Persists one-time license activation across sessions and app restarts.
    public sealed class ActivationState
    {
        private readonly string _stateFile;
        private bool? _cachedActivated;

        public ActivationState(IWebHostEnvironment env)
        {
            var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(dataDir);
            _stateFile = Path.Combine(dataDir, "activation.state");
        }

        public bool IsActivated()
        {
            if (_cachedActivated.HasValue)
                return _cachedActivated.Value;

            _cachedActivated = File.Exists(_stateFile);
            return _cachedActivated.Value;
        }

        public void MarkActivated()
        {
            if (!File.Exists(_stateFile))
            {
                File.WriteAllText(
                    _stateFile,
                    $"ActivatedUtc={DateTime.UtcNow:O}{Environment.NewLine}"
                );
            }

            _cachedActivated = true;
        }
    }
}
