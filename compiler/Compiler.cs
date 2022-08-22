using System;
using System.Collections.Generic;
using Ink;

namespace Ink
{
    public class Compiler
    {
        public class Options
        {
            public string sourceFilename;
            public List<string> pluginDirectories;
            public bool countAllVisits;
            public Ink.ErrorHandler errorHandler;
            public Ink.IFileHandler fileHandler;
        }

        public Parsed.Story parsedStory {
            get {
                return _parsedStory;
            }
        }

        public Compiler (string inkSource, Options options = null)
        {
            _inputString = inkSource;
            _options = options ?? new Options();
            if( _options.pluginDirectories != null )
                _pluginManager = new PluginManager (_options.pluginDirectories);
        }

        public Parsed.Story Parse()
        {
            _parser = new InkParser(_inputString, _options.sourceFilename, OnParseError, _options.fileHandler);
            _parsedStory = _parser.Parse();
            return _parsedStory;
        }

        public Runtime.Story Compile ()
        {
            if( _pluginManager != null )
                _inputString = _pluginManager.PreParse(_inputString);

            Parse();

            if( _pluginManager != null )
                _parsedStory = _pluginManager.PostParse(_parsedStory);

            if (_parsedStory != null && !_hadParseError) {

                _parsedStory.countAllVisits = _options.countAllVisits;

                _runtimeStory = _parsedStory.ExportRuntime (_options.errorHandler);

                if( _pluginManager != null )
                    _runtimeStory = _pluginManager.PostExport (_parsedStory, _runtimeStory);
            } else {
                _runtimeStory = null;
            }

            return _runtimeStory;
        }

        public struct DebugSourceRange
        {
            public int length;
            public Runtime.DebugMetadata debugMetadata;
            public string text;
        }

        // Need to wrap the error handler so that we know
        // when there was a critical error between parse and codegen stages
        void OnParseError (string message, ErrorType errorType)
        {
            if( errorType == ErrorType.Error )
                _hadParseError = true;
            
            if (_options.errorHandler != null)
                _options.errorHandler (message, errorType);
            else
                throw new System.Exception(message);
        }

        string _inputString;
        Options _options;


        InkParser _parser;
        Parsed.Story _parsedStory;
        Runtime.Story _runtimeStory;

        PluginManager _pluginManager;

        bool _hadParseError;
    }
}
