// Java payloads shared by the capacity scripts.
//
// Every payload prints a unique marker as its LAST line. That single convention
// is what makes body-level assertions possible: if the marker is present, the
// program compiled, ran to completion, and was not killed part-way through.
//
// Why body assertions and not HTTP status: Piston answers HTTP 200 even when it
// SIGKILLs a student's program for exceeding run_timeout, and
// JavaExecuteResultClassifier maps that kill to RUNTIME_ERROR — the same value a
// real NullPointerException produces. So neither the HTTP status nor any
// server-side metric can tell "we timed out your working code" from "you wrote a
// bug". The marker can.

/**
 * Minimal program — the capacity probe. Measures the floor: JVM startup plus the
 * in-memory compile that JEP 330 source mode performs on every job.
 */
export function pHello(token) {
  const marker = `OK-${token}`
  return {
    name: 'hello',
    marker,
    source: `public class Main {
  public static void main(String[] args) {
    System.out.println("${marker}");
  }
}`,
  }
}

/**
 * A realistic day-2 program: a helper method, a loop, string building. Used to
 * confirm pHello isn't flattering the numbers — if typical student code costs
 * materially more CPU than hello-world, the capacity table must be built from
 * this one instead.
 */
export function pTypical(token) {
  const marker = `OK-${token}`
  return {
    name: 'typical',
    marker,
    source: `public class Main {
  static String describe(int n) {
    if (n % 2 == 0) {
      return n + " is even";
    }
    return n + " is odd";
  }

  public static void main(String[] args) {
    String report = "";
    int total = 0;
    for (int i = 1; i <= 12; i++) {
      total = total + i;
      report = report + describe(i) + "; ";
    }
    System.out.println(report.trim());
    System.out.println("total=" + total);
    System.out.println("${marker}");
  }
}`,
  }
}

/**
 * An infinite loop — what students will actually write on day 2 (the seed data
 * has a while-loop quiz whose expected answer is literally "infinite loop").
 *
 * This is the positive control for the SIGKILL counter: it must NEVER print its
 * marker and must always come back killed. If piston-sweep reports sigkill=0 for
 * this payload, the SIGKILL detection itself is broken and a sigkill=0 result on
 * the other payloads means nothing.
 */
export function pSpin(token) {
  const marker = `OK-${token}`
  return {
    name: 'spin',
    marker,
    source: `public class Main {
  public static void main(String[] args) {
    long spun = 0;
    while (true) {
      spun++;
    }
  }
}`,
  }
}

const BUILDERS = {
  hello: pHello,
  typical: pTypical,
  spin: pSpin,
}

/**
 * Resolve a payload by name, for scripts that take PAYLOAD as an env var.
 * Throws on an unknown name rather than silently falling back — a run that
 * measured a different payload than you think is worse than no run.
 */
export function buildPayload(name, token) {
  const builder = BUILDERS[name]
  if (!builder) {
    throw new Error(`unknown PAYLOAD "${name}" — expected one of: ${Object.keys(BUILDERS).join(', ')}`)
  }
  return builder(token)
}
