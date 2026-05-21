namespace shmtu.cas.captcha;

public sealed record CaptchaAnswer(string Value, CaptchaAnswerKind Kind = CaptchaAnswerKind.Answer);
