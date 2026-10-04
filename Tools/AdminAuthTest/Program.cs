using System;
using PrisonersOfOmar.Gameplay;

// Test vector from Docs/ITERATION2.md (NOT the real admin password).
static class Program
{
    static int Main()
    {
        const string pw = "test-test test";   // normalises to TESTTESTTEST
        const string wantDk = "f605b215e97fd4b6e2755a3c1adaec8a240a7d42ddd8ee66568024ece99ab9ef";
        const string wantVerifier = "ddd7b8db21cf4a5c3882feb9854a7e854d74b58018b748f0075ed3d6d9bd2073";
        byte[] dk = AdminAuth.DeriveKey(pw);
        string dkHex = AdminAuth.ToHex(dk);
        string ver = AdminAuth.VerifierOf(dk);
        bool ok = dkHex == wantDk && ver == wantVerifier && AdminAuth.Verify(dk, wantVerifier) && !AdminAuth.Verify(dk);
        Console.WriteLine("normalised: " + AdminAuth.Normalize(pw));
        Console.WriteLine("dk:         " + dkHex + (dkHex == wantDk ? "  OK" : "  MISMATCH"));
        Console.WriteLine("verifier:   " + ver + (ver == wantVerifier ? "  OK" : "  MISMATCH"));
        Console.WriteLine(ok ? "PASS" : "FAIL");
        return ok ? 0 : 1;
    }
}
