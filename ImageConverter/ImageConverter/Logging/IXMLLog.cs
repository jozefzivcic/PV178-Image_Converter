﻿namespace ImageConverter.Logging
{
    public interface IXMLLog
    {
        void Info(string message);
        void Debug(string message);
        void Error(string message);
    }
}
