namespace TaxReader.Domain.Enums;

public enum AuditAction
{
    AccountDeleted,
    TokensGranted,
    TokensRevoked,
    RefreshTokenReplayDetected,
    ClassificationRuleCreated,
    DataExportRequested,
    DataExportDownloaded,
    ItemCorrected
}
