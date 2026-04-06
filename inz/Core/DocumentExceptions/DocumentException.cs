namespace inz.Core.DocumentExceptions
{
    public abstract class DocumentException : Exception
    {
        protected DocumentException(string message) : base(message) { }

        protected DocumentException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class InvalidDocumentException : DocumentException
    {
        public InvalidDocumentException(string message) : base(message) { }
    }

    public class EmptyDocumentException : InvalidDocumentException
    {
        public EmptyDocumentException(string message) : base(message) { }
    }

    public class UnsupportedDocumentTypeException : InvalidDocumentException
    {
        public UnsupportedDocumentTypeException(string message) : base(message) { }
    }

    public class DocumentProcessingFailure : DocumentException
    {
        public string FileName { get; }

        public DocumentProcessingFailure(string message, string fileName, Exception innerException)
            : base(message, innerException)
        {
            FileName = fileName;
        }
    }
}
