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
        public string DocumentName { get; }
        public Exception InnerException { get; }
        public DocumentProcessingFailure(string message, string documentName, Exception innerException)
            : base(message, innerException)
        {
            DocumentName = documentName;
            InnerException = innerException;
        }
    }

    public class DocumentNotFoundException : DocumentException
    {
        public DocumentNotFoundException(string message) : base(message) { }
    }

    public class DocumentUnavailableException : DocumentException
    {
        public string DocumentName { get; }
        public DocumentUnavailableException(string message, string documentName) : base(message)
        {
            DocumentName = documentName;
        }
    }

    public class DocumentRetrievalFailureException : DocumentException
    {
        public string DocumentName { get; }
        public Exception InnerException { get; }
        public DocumentRetrievalFailureException(string message, string documentName, Exception innerException) : base(message)
        {
            DocumentName = documentName;
            InnerException = innerException;
        }
    }
}
