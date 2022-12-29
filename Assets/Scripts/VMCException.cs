using System;
using System.Runtime.Serialization;

[Serializable()]
public class VMCException : Exception
{
    public VMCException() : base() { }
    public VMCException(string message) : base(message) { }
    public VMCException(string message, Exception innerException) : base(message, innerException) { }
    protected VMCException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

[Serializable()]
public class CalibrationFailedException : VMCException
{
    public CalibrationFailedException() : base() { }
    public CalibrationFailedException(string message) : base(message) { }
    public CalibrationFailedException(string message, Exception innerException) : base(message, innerException) { }
    protected CalibrationFailedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
