namespace Flygcert.LetsEncryptAzureCdn.Exceptions
{
    public class ChallengeValidationFailedException : Exception
    {
        public ChallengeValidationFailedException() { }
        public ChallengeValidationFailedException(string message) : base(message) { }
        public ChallengeValidationFailedException(string message, Exception inner) : base(message, inner) { }
    }
}
