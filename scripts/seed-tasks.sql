-- ============================================================================
-- seed-tasks.sql — the 34 BootIT assignments + assignment sets, migrated from
-- the frontend's hardcoded bundle (cobblersFrontend/src/lib/assignments.ts).
--
-- Usage (local or VM — schema must already exist via `dotnet ef database update`):
--
--     psql "$CONNECTION_STRING" -f scripts/seed-tasks.sql
--
-- Safe to re-run (idempotent):
--   * assignment_set / assignment rows are UPSERTed — content edits here overwrite the DB.
--   * assignment.sample_solution_json is only set on FIRST insert and never
--     overwritten on re-run, so solutions authored directly in the DB survive.
--   * Slugs that are no longer in this file are deleted (dev-safe: no real data).
--   * assignment_set_assignment memberships are rebuilt from scratch each run.
--
-- Conventions (see SCHEMA.md):
--   * assignment.id is DB-assigned — never written here. All references go through
--     assignment.slug (stable natural key, unique).
--   * kind is lowercase text: 'code' | 'predict' | 'project'.
--   * content_json holds the kind-specific payload (camelCase keys), safe to
--     send to students. Grading rules live in grading_json (never sent).
--   * lesson_json is optional teaching blocks ([{kind:"text",text}|{kind:"code",code}])
--     shown above the task — wire field `lesson`, sibling of hint/content.
--   * grading_json is the rule DSL evaluated by the backend's AssignmentGrader:
--       {"all":[...]} / {"any":[...]} / {"not":...} /
--       {"target":"stdout"|"code","op":"contains"|"containsLine","value":...} /
--       {"target":...,"op":"regex","pattern":...,"flags":"i"?} /
--       {"op":"nonEmptyStdout"}
--     NULL = not auto-gradable (projects, NIM) or graded generically (predict).
-- ============================================================================

BEGIN;

-- ────────────────────────────── assignment sets ────────────────────────────

INSERT INTO assignment_set (assignment_set_id, display_title) VALUES
  ('day1-2026',                     'BootIT Day 1 — 2026'),
  ('day2-2026',                     'BootIT Day 2 — 2026'),
  ('day3-2026',                     'BootIT Day 3 — 2026'),
  ('all-assignments-for-solo-2026', 'BootIT — All Tasks (Solo) 2026')
ON CONFLICT (assignment_set_id) DO UPDATE SET display_title = EXCLUDED.display_title;

-- Drop memberships before deleting obsolete assignment rows (FK).
DELETE FROM assignment_set_assignment
WHERE assignment_set_id IN ('day1-2026', 'day2-2026', 'day3-2026', 'all-assignments-for-solo-2026');

-- Dev-only: no real submissions yet. Clear so obsolete assignment rows can go.
DELETE FROM submission;

-- ──────────────────────────────── assignments ──────────────────────────────

INSERT INTO assignment (slug, kind, title, description, hint, lesson_json, content_json, sample_solution_json, grading_json) VALUES

-- ─────────────────────────── DAY 1 — basics ───────────────────────────
(
  'hello-itu', 'code', 'Hello ITU',
  $txt$Now it is your turn: print a sentence to say hello to your new university. Print exactly: Hello ITU!$txt$,
  $txt$System.out.println("Hello ITU!");$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Printing a message is the most basic thing every programming language can do. In Java it takes a class, a main method, and one print statement:$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$class Hello {
    public static void main(String[] args) {
        System.out.println("Hello World!");
    }
}$java$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        // Say hello to ITU
    }
}
$java$),
  NULL,
  $j${"target": "stdout", "op": "containsLine", "value": "Hello ITU!"}$j$::jsonb
),
(
  'print-three-values', 'code', 'Print three values',
  $txt$Print three things, each on its own line:
1. A greeting with your name, like "Hello, my name is Aiting!"
2. The year you were born — a whole number
3. How many years you have lived in Copenhagen — with a decimal point (1.0 for exactly one year, 3.5 for three and a half)$txt$,
  $txt$Three println statements. 1996 is an int, 3.5 is a double, "Hello!" is a String.$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$println can print more than text — whole numbers and decimal numbers work too. Notice that numbers need no quotes:$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$System.out.println("Hello World!");
System.out.println(42);
System.out.println(3.14);$java$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        // 1) a greeting  2) your birth year  3) years in Copenhagen (with a decimal)
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+$"},
    {"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+\\.\\d+$"},
    {"target": "stdout", "op": "regex", "pattern": "[A-Za-z]{2,}"}
  ]}$j$::jsonb
),
(
  'use-variables', 'code', 'Use variables',
  $txt$Print the same three values as before, but this time store each one in a variable first, then print the variable. Pick the right type: String for the greeting, int for the year, double for the years in Copenhagen.$txt$,
  $txt$String greeting = "Hello, my name is …!"; then System.out.println(greeting);$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$The same values can be stored in variables first. A variable has a type, a name, and a value:$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$int x = 42;
System.out.println(x);

String s = "hi";
System.out.println(s);

double d = 3.14;
System.out.println(d);

boolean b = true;
System.out.println(b);$java$),
    jsonb_build_object('kind', 'text', 'text', $txt$The four basic types:
int — whole numbers: 1, 0, -420, 2147483647
String — text in quotes: "hi", "hello world", "14b"
double — decimal numbers: 1.5, 3.1415, -27.15, 1.0
boolean — true or false$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        // Declare a String, an int and a double — then print the variables
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bString\\s+\\w+\\s*="},
    {"target": "code", "op": "regex", "pattern": "\\bint\\s+\\w+\\s*="},
    {"target": "code", "op": "regex", "pattern": "\\bdouble\\s+\\w+\\s*="},
    {"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+$"},
    {"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+\\.\\d+$"}
  ]}$j$::jsonb
),
(
  'variable-assignment', 'code', 'Variable assignment',
  $txt$Use one int variable called age (starting at 27) to print "ITU is 27 years old." Then update the SAME variable with age = age + 1 and use it again to print "Next year ITU will be 28 years old."$txt$,
  $txt$System.out.print(...) keeps printing on the same line; println(...) ends it.$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$A variable can be given a new value later — that is why it is called a variable. The same variable then prints a different value:$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$int year = 2026;
System.out.print("The year is ");
System.out.println(year);   // The year is 2026

year = year + 1;
System.out.print("The year is now ");
System.out.println(year);   // The year is now 2027$java$),
    jsonb_build_object('kind', 'text', 'text', $txt$Fun fact: ITU is the youngest university in Denmark, founded in 1999 — it turns 27 in 2026.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int age = 27;
        // Print both sentences — update age in between
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "contains", "value": "ITU is 27 years old."},
    {"target": "stdout", "op": "contains", "value": "Next year ITU will be 28 years old."},
    {"target": "code", "op": "regex", "pattern": "\\bage\\s*=\\s*age\\s*\\+\\s*1\\b|\\bage\\s*\\+\\+|\\bage\\s*\\+=\\s*1\\b"}
  ]}$j$::jsonb
),
(
  'operators', 'code', 'Operators',
  $txt$Snack run at Cafe Analog: you buy one kanelsnegl (12 kr) for each of your 4 study-group friends, plus one juice (30 kr) to share. Print the total price (78). Then print what each of the 4 friends pays if you split the total evenly (19.5).$txt$,
  $txt$Split with a decimal: total / 4.0 — dividing by the int 4 throws the .5 away.$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Java can calculate with + (plus), - (minus), * (multiply) and / (divide):$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$System.out.println(3 + 3);   // 6

int x = 2;
System.out.println(x * x);   // 4

int y = 6;
System.out.println(y / 3);   // 2$java$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int snegl = 12;
        int juice = 30;
        int friends = 4;
        // Print the total, then the price per friend
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "contains", "value": "78"},
    {"target": "stdout", "op": "contains", "value": "19.5"},
    {"target": "code", "op": "contains", "value": "*"},
    {"target": "code", "op": "contains", "value": "/"}
  ]}$j$::jsonb
),
(
  'string-concatenation', 'code', 'String concatenation',
  $txt$The starter prints "Hello my friend!". Ask the person sitting next to you for their name, then modify the code to greet them personally: Hello my friend, {Name}!$txt$,
  $txt$Add a String name = "…"; and concatenate it after "friend, ".$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$+ between Strings glues them together — this is called concatenation:$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$String hi = "Hello ";
String world = "World!";
String greet = hi + world;

System.out.println(greet);   // Hello World!$java$),
    jsonb_build_object('kind', 'text', 'text', $txt$It also works between Strings and numbers:$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$int year = 2026;
System.out.println("The year is " + year);$java$)
  ),
  jsonb_build_object(
    'starter', $java$public class Hello {
    public static void main(String[] args) {
        String first = "Hello";
        String second = "my";
        String third = "friend";
        System.out.println(first + " " + second + " " + third + "!");
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "regex", "pattern": "Hello my friend, .+!"},
    {"target": "code", "op": "contains", "value": "+"}
  ]}$j$::jsonb
),
(
  'kroner-to-euro', 'code', 'Kroner to euro',
  $txt$Modify the code so it converts the opposite way: from kroner to euro. For the 17 kr cappuccino, print: "17 dkk corresponds to 2.2818791946308725 euro." (All the decimals are fine — that is just how doubles print.)$txt$,
  $txt$Divide instead of multiply: dkk / 7.45$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$During the break you meet a friend at ITU's own café, Cafe Analog. If you buy 5 coffee tickets in the Analog app, a cappuccino costs only 17 kr. Your friend says that is very cheap — but you want to see it in euro. This code converts the other way, from euro to kroner:$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$class Valuta {
    public static void main(String[] args) {
        int eur = 100;
        double dkk = eur * 7.45;
        System.out.println(eur + " euro corresponds to " + dkk + " kr.");
    }
}$java$)
  ),
  jsonb_build_object(
    'starter', $java$class Valuta {
    public static void main(String[] args) {
        int eur = 100;
        double dkk = eur * 7.45;
        System.out.println(eur + " euro corresponds to " + dkk + " kr.");
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "contains", "value": "17"},
    {"target": "stdout", "op": "contains", "value": "corresponds to"},
    {"target": "stdout", "op": "contains", "value": "euro"},
    {"target": "stdout", "op": "regex", "pattern": "2\\.28"},
    {"target": "code", "op": "regex", "pattern": "/\\s*7\\.45"}
  ]}$j$::jsonb
),
(
  'functions', 'code', 'Functions',
  $txt$Write a function that takes TWO parameters: a String item and an int price in dkk. It prints "A cup of {item} cost {price in euro} euros." Call it twice from main with different drinks — say a Cappuccino at 17 kr and a Matcha Latte at 20 kr.$txt$,
  $txt$Two parameters need a comma: static void cafeEuroPrice(String item, int dkk) { … }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Functions let us reuse code and give a snippet a clear responsibility. This does exactly the same as the previous exercise, wrapped in a function:$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$class Valuta {
    static void dkk2eur() {
        double dkk = 100;
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    public static void main(String[] args) {
        dkk2eur();
    }
}$java$),
    jsonb_build_object('kind', 'text', 'text', $txt$But this function always converts 100 kr. With a parameter, the same function works for any value:$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$static void dkk2eur(double dkk) {
    double eur = dkk / 7.45;
    System.out.println(dkk + " kr corresponds to " + eur + " euro");
}

public static void main(String[] args) {
    dkk2eur(100);
    dkk2eur(17);
}$java$)
  ),
  jsonb_build_object(
    'starter', $java$class Valuta {

    // Declare your function here

    public static void main(String[] args) {
        System.out.println("What would you like to order?");
        // cafeEuroPrice("Cappuccino", 17);
        System.out.println("Anything else?");
        // cafeEuroPrice("Matcha Latte", 20);
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "static\\s+void\\s+\\w+\\s*\\(\\s*String\\s+\\w+\\s*,\\s*(?:int|double)\\s+\\w+\\s*\\)"},
    {"target": "stdout", "op": "regex", "pattern": "A cup of .+? cost .+? euros\\."}
  ]}$j$::jsonb
),
(
  'your-semester-in-ects', 'code', 'Your semester in ECTS',
  $txt$Write a function printCourse(String name, int ects, int semester) that prints "{name} ({ects} ECTS) is in semester {semester}." Call it from main for at least two courses of your new programme — e.g. printCourse("Software Design", 15, 1) and printCourse("Mobile App Development", 15, 2).$txt$,
  $txt$Three parameters, two commas: static void printCourse(String name, int ects, int semester)$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$At ITU every course is worth ECTS points, and a full semester adds up to 30 ECTS — for example two 7.5 ECTS courses plus a 15 ECTS project. A function with several parameters can print any course the same way.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$class StudyPlan {

    // Declare printCourse here

    public static void main(String[] args) {
        // Print at least two of your courses
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "static\\s+void\\s+\\w+\\s*\\(\\s*String\\s+\\w+\\s*,\\s*int\\s+\\w+\\s*,\\s*int\\s+\\w+\\s*\\)"},
    {"target": "stdout", "op": "regex", "pattern": ".+ \\(\\d+ ECTS\\) is in semester \\d+\\."}
  ]}$j$::jsonb
),

-- ─────────────────────────── DAY 2 — conditionals, loops, input ───────────────────────────
(
  'is-it-daytime', 'code', 'Is it daytime?',
  $txt$Given an hour (0–23), print "Yes, it is daytime!" from 08:00 onward, otherwise "No, it is nighttime."$txt$,
  $txt$Use if (time >= 8) { ... } else { ... }$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int time = 14;
        // Print daytime or nighttime
    }
}
$java$),
  NULL,
  $j${"target": "stdout", "op": "regex", "pattern": "daytime|nighttime", "flags": "i"}$j$::jsonb
),
(
  'big-or-small', 'code', 'Big or small',
  $txt$Write a parameterised function bigsmall(int n) that prints "<n> is a big number" when n > 99, otherwise "<n> is a small number".$txt$,
  $txt$static void bigsmall(int number) { if (number > 99) ... }$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$public class Number {
    static void bigsmall(int number) {
        // print big or small
    }

    public static void main(String[] args) {
        bigsmall(2);
        bigsmall(999);
    }
}
$java$),
  NULL,
  $j${"target": "stdout", "op": "regex", "pattern": "(big|small) number", "flags": "i"}$j$::jsonb
),
(
  'even-or-odd', 'code', 'Even or odd',
  $txt$Write a function even(int n) that RETURNS a boolean (true if n is even). Use it to print "<n> is even" or "<n> is not even".$txt$,
  $txt$Even numbers are divisible by 2: number % 2 == 0$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$public class Numbers {
    static boolean even(int number) {
        // return whether number is even
        return false;
    }

    public static void main(String[] args) {
        int number = 2;
        // use even(number) to print the result
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "boolean\\s+even"},
    {"target": "stdout", "op": "regex", "pattern": "is (not )?even", "flags": "i"}
  ]}$j$::jsonb
),
(
  'time-of-day', 'code', 'Time of day',
  $txt$Write daypart(int time) that RETURNS a String: 0 midnight, 1–5 night, 6–11 morning, 12 noon, 13–17 afternoon, 18+ evening. Print "It is <daypart>".$txt$,
  $txt$Use an if / else-if chain that returns a String.$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$public class TimeOfDay {
    static String daypart(int time) {
        // return the part of the day
        return "";
    }

    public static void main(String[] args) {
        System.out.println("It is " + daypart(9));
    }
}
$java$),
  NULL,
  $j${"target": "stdout", "op": "regex", "pattern": "morning|noon|afternoon|evening|night|midnight", "flags": "i"}$j$::jsonb
),
(
  'multiplication-series', 'code', 'Multiplication series',
  $txt$Using a loop, print the multiplication series for 5: 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 (one per line is fine).$txt$,
  $txt$for (int i = 1; i <= 10; i++) { System.out.println(series * i); }$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int series = 5;
        // Print series * 1 ... series * 10 with a loop
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "containsLine", "value": "50"},
    {"target": "stdout", "op": "containsLine", "value": "45"}
  ]}$j$::jsonb
),
(
  'guess-the-number', 'code', 'Guess the number',
  $txt$A number-guessing game: pick a secret with Random, read guesses with a Scanner in a while loop, and print "Too low" / "Too high" until correct.$txt$,
  $txt$Scanner scanner = new Scanner(System.in); while (guess != secret) { guess = scanner.nextInt(); ... }$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$import java.util.*;

public class Guess {
    public static void main(String[] args) {
        Random random = new Random();
        Scanner scanner = new Scanner(System.in);
        int secret = random.nextInt(100);
        int tries = 0;
        int guess = -1;
        // Loop until the guess equals secret; print Too low / Too high
    }
}
$java$,
    'stdin', $txt$50
25
37
31
34
$txt$),
  NULL,
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "Scanner"},
    {"target": "code", "op": "regex", "pattern": "while"},
    {"target": "code", "op": "regex", "pattern": "nextInt"}
  ]}$j$::jsonb
),
(
  'nim-bonus', 'code', 'NIM (bonus)',
  $txt$Bonus: build NIM for 2 players. Matches on the table; each turn a player takes 1–3 (never more than what is left). Whoever takes the last match wins. Read moves with a Scanner.$txt$,
  $txt$Track remaining matches in a loop; validate the move is 1–3 and not more than remaining.$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$import java.util.*;

public class Nim {
    public static void main(String[] args) {
        Scanner scanner = new Scanner(System.in);
        int matches = 13;
        // Take turns until the matches run out
    }
}
$java$),
  NULL,
  NULL
),

-- ─────────────────────────── DAY 2 — predict-the-output loop quizzes ───────────────────────────
-- Predict tasks carry no grading_json: they are graded generically from
-- content_json.expectedOutput / accept[] (see SCHEMA.md).
(
  'while-loop-quiz-1', 'predict', 'While Loop Quiz 1',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 10;
while (i > 0) {
    System.out.println(i);
    i = i - 1;
}$java$,
    'expectedOutput', $txt$10
9
8
7
6
5
4
3
2
1$txt$),
  NULL,
  NULL
),
(
  'while-loop-quiz-2', 'predict', 'While Loop Quiz 2',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 1;
while (i <= 10) {
    System.out.println(i);
    i = i + 2;
}$java$,
    'expectedOutput', $txt$1
3
5
7
9$txt$),
  NULL,
  NULL
),
(
  'while-loop-quiz-3', 'predict', 'While Loop Quiz 3',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 1;
while (i < 100) {
    System.out.println(i);
    i = i * 2;
}$java$,
    'expectedOutput', $txt$1
2
4
8
16
32
64$txt$),
  NULL,
  NULL
),
(
  'while-loop-quiz-4', 'predict', 'While Loop Quiz 4',
  $txt$Careful with this one! Predict what it prints — some loops never stop.$txt$,
  $txt$What is 1 × 1? Does i ever change?$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 1;
while (i < 42) {
    System.out.println(i);
    i = i * i;
}$java$,
    'expectedOutput', $txt$infinite loop$txt$,
    'accept', jsonb_build_array($txt$infinite$txt$, $txt$never stops$txt$, $txt$never ends$txt$, $txt$forever$txt$, $txt$loops forever$txt$, $txt$does not stop$txt$, $txt$doesn't stop$txt$)),
  NULL,
  NULL
),
(
  'while-loop-quiz-5', 'predict', 'While Loop Quiz 5',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 0;
while (i <= 15) {
    System.out.println(i);
    i = i + 3;
}$java$,
    'expectedOutput', $txt$0
3
6
9
12
15$txt$),
  NULL,
  NULL
),
(
  'while-loop-quiz-6', 'predict', 'While Loop Quiz 6',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 64;
while (i >= 2) {
    System.out.println(i);
    i = i / 2;
}$java$,
    'expectedOutput', $txt$64
32
16
8
4
2$txt$),
  NULL,
  NULL
),
(
  'for-loop-quiz-1', 'predict', 'For Loop Quiz 1',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 10; i > 0; i = i - 2) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$10
8
6
4
2$txt$),
  NULL,
  NULL
),
(
  'for-loop-quiz-2', 'predict', 'For Loop Quiz 2',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 1; i < 10; i = i + 3) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$1
4
7$txt$),
  NULL,
  NULL
),
(
  'for-loop-quiz-3', 'predict', 'For Loop Quiz 3',
  $txt$Careful with this one! Predict what it prints — some loops never stop.$txt$,
  $txt$What is 1 × 1? Does i ever grow?$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 1; i < 10; i = i * i) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$infinite loop$txt$,
    'accept', jsonb_build_array($txt$infinite$txt$, $txt$never stops$txt$, $txt$never ends$txt$, $txt$forever$txt$, $txt$loops forever$txt$, $txt$does not stop$txt$, $txt$doesn't stop$txt$)),
  NULL,
  NULL
),
(
  'for-loop-quiz-4', 'predict', 'For Loop Quiz 4',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 0; i <= 15; i = i + 3) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$0
3
6
9
12
15$txt$),
  NULL,
  NULL
),
(
  'for-loop-quiz-5', 'predict', 'For Loop Quiz 5',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 1; i <= 10000; i = i * 10) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$1
10
100
1000
10000$txt$),
  NULL,
  NULL
),
(
  'for-loop-quiz-6', 'predict', 'For Loop Quiz 6',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 64; i >= 2; i = i / 2) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$64
32
16
8
4
2$txt$),
  NULL,
  NULL
),

-- ─────────────────────────── DAY 3 — classes & objects (harness-graded) ───────────────────────────
(
  'person-class', 'code', 'Person class',
  $txt$Warm-up: make a Person class with fields name and age, a constructor Person(String n, int a), display() that prints "Niek (25 years old)", and birthday() that adds a year.$txt$,
  $txt$display(): System.out.println(name + " (" + age + " years old)");$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$class Person {
    // fields: name, age

    // constructor Person(String n, int a)

    // void display()  -> "Niek (25 years old)"

    // void birthday() -> age + 1
}
$java$,
    'harness', jsonb_build_object(
      'files', jsonb_build_array(jsonb_build_object('name', 'Main.java', 'content', $java$public class Main {
    public static void main(String[] args) {
        Person p = new Person("Niek", 25);
        p.display();
        p.birthday();
        p.display();
    }
}
$java$)),
      'entryClass', 'Main'),
    'solutionFile', 'Person.java'),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "containsLine", "value": "Niek (25 years old)"},
    {"target": "stdout", "op": "containsLine", "value": "Niek (26 years old)"}
  ]}$j$::jsonb
),
(
  'flight-ticket-class', 'code', 'FlightTicket class',
  $txt$Make a FlightTicket class: fields from, to, price; constructor (f, t, p); show() prints "CPH --> JFK (7500 DKK)"; discount() takes 500 DKK off. Make sure discount() can't be abused (price must never go negative).$txt$,
  $txt$In discount(), only subtract if the price stays >= 0.$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$class FlightTicket {
    // fields: from, to, price

    // constructor FlightTicket(String f, String t, int p)

    // void show()     -> "CPH --> JFK (7500 DKK)"

    // void discount() -> 500 DKK off, but never below 0
}
$java$,
    'harness', jsonb_build_object(
      'files', jsonb_build_array(jsonb_build_object('name', 'Main.java', 'content', $java$public class Main {
    public static void main(String[] args) {
        FlightTicket t = new FlightTicket("CPH", "JFK", 7500);
        t.show();
        t.discount();
        t.show();
        for (int i = 0; i < 20; i++) { t.discount(); }
        t.show();
    }
}
$java$)),
      'entryClass', 'Main'),
    'solutionFile', 'FlightTicket.java'),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "containsLine", "value": "CPH --> JFK (7500 DKK)"},
    {"target": "stdout", "op": "containsLine", "value": "CPH --> JFK (7000 DKK)"},
    {"not": {"target": "stdout", "op": "regex", "pattern": "-\\d+\\s*DKK"}}
  ]}$j$::jsonb
),
(
  'container-class', 'code', 'Container class',
  $txt$Make a Container class: fields id, amount, max; constructor Container(String i, int max) (amount starts at 0); show() prints "Container: AX35 (23/30)"; addCargo(int a) adds boxes. Make sure the container can't be over-filled.$txt$,
  $txt$In addCargo, only add if amount + a <= max (mirror the Account guard pattern).$txt$,
  NULL,
  jsonb_build_object(
    'starter', $java$class Container {
    // fields: id, amount, max

    // constructor Container(String i, int max)  -> amount = 0

    // void show()           -> "Container: AX35 (23/30)"

    // void addCargo(int a)  -> add boxes, but never above max
}
$java$,
    'harness', jsonb_build_object(
      'files', jsonb_build_array(jsonb_build_object('name', 'Main.java', 'content', $java$public class Main {
    public static void main(String[] args) {
        Container c = new Container("AX35", 30);
        c.addCargo(23);
        c.show();
        c.addCargo(40);
        c.show();
    }
}
$java$)),
      'entryClass', 'Main'),
    'solutionFile', 'Container.java'),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "containsLine", "value": "Container: AX35 (23/30)"},
    {"not": {"target": "stdout", "op": "regex", "pattern": "\\((?:3[1-9]|[4-9]\\d|\\d{3,})/30\\)"}}
  ]}$j$::jsonb
),

-- ─────────────────────────── DAY 3 — mini-projects (multi-file upload) ───────────────────────────
-- No grading_json: projects are manually reviewed (Submission.Passed stays null).
(
  'build-a-tree', 'project', 'Build a Tree',
  $txt$Mini-project: model a growing (and eventually dying, occasionally blooming) tree.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'brief', $txt$Build a Tree®

You've started a green company — Build a Tree®. Build an awesome piece of Java in VS Code.

1) A class representing a Tree, with display() to show current info, and grow() so it can grow.
2) Work with at least a height and an age, so it can grow and get older. Parameterise growth_rate and max_age so the tree can die — and track whether it is alive or dead (print this too).
3) Make the tree flower every 5th year, but NOT every 20th year — unless we enter a new century!

Sketch:
> Your tree is currently 0 years old,        // display()
  It has reached the height of 1.0cm.
> And your tree just grew a year older!       // grow()
  ...
> Your tree is currently 5 years old,
  It has reached the height of 32.0cm, and it is currently blooming.
  ...
> The tree has died                           // grow()
> The tree is dead, and reached the age 11 with a height of 2048.0cm
> The tree is already dead.                   // grow() again

Develop it in VS Code, then upload your .java files here to run them.$txt$,
    'requiredClasses', jsonb_build_array('Tree'),
    'entryClass', 'Main'),
  NULL,
  NULL
),
(
  'grandpas-time-machine', 'project', 'Grandpa''s Time Machine',
  $txt$Mini-project: a text-based time machine that travels between years.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'brief', $txt$Grandpa's Time Machine

Make a text-based time machine for grandpa Rick (who thinks it's 3013).

1) Move back and forward in time with the same method taking a destination year as a parameter, and tell him which year he ends up in. Announce every year that passes while travelling, and when he reaches the destination.
2) Tell him when he passes a year with an important historical event.
3) Tell him whenever it is a leap year — and be precise (handle the century rule).

Sketch (travelling 2016 → 2020):
Tim3M4chin3: current year is now 2016
Tim3M4chin3: A leap year just happened WoOoOOoOW!
Tim3M4chin3: Current year is now 2017
Tim3M4chin3: Current year is now 2018
Tim3M4chin3: A lot of awesome people went to BootIT
Tim3M4chin3: Current year is now 2019
Tim3M4chin3: You arrived to your destination: 2020

Hint: model the machine as an object — store the current year as a field; one method for each direction (or one smart loop). Develop in VS Code, then upload your .java files here.$txt$,
    'requiredClasses', jsonb_build_array('TimeMachine'),
    'entryClass', 'Main'),
  NULL,
  NULL
),
(
  'grandmas-blackmarket-kitchen', 'project', 'Grandma''s Blackmarket Kitchen',
  $txt$Mini-project: a catering planner that assigns menus and dodges the police.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'brief', $txt$Grandma's Blackmarket Kitchen

Grandma caters (tax-free!) with two menus. Help her plan orders.

1) Input the total number of people and how many are picky eaters.
2) Refuse police stings: 8 people with 7 picky (the police), and 4 people with 7 picky (the grandson — an impossible order). Print an error for those.
3) Picky eaters always get the first menu; non-picky get a randomly chosen menu.
4) Print the order summary, e.g.:
Grandma: I want to cook the following 10 menus to you:
7x 1. Tarteletter, 2. Stegt flæsk m. persillesovs, 3. citronfromage
3x 1. red cabbage salad, 2. curry chicken, 3. rødgrød m. fløde

Hint: use if-else for the bad orders; subtract picky eaters from the total and loop for the rest. Random: int r = (new Random()).nextInt(6);

Develop in VS Code, then upload your .java files here.$txt$,
    'requiredClasses', jsonb_build_array('Kitchen'),
    'entryClass', 'Main'),
  NULL,
  NULL
)
ON CONFLICT (slug) DO UPDATE SET
  kind          = EXCLUDED.kind,
  title         = EXCLUDED.title,
  description   = EXCLUDED.description,
  hint          = EXCLUDED.hint,
  lesson_json   = EXCLUDED.lesson_json,
  content_json  = EXCLUDED.content_json,
  grading_json  = EXCLUDED.grading_json;
  -- sample_solution_json intentionally NOT updated: solutions authored in the
  -- DB after the first run must survive re-seeding.

-- Remove slugs that are no longer in the frontend mock (cafe-era Day 1, etc.).
DELETE FROM assignment
WHERE slug NOT IN (
  'hello-itu',
  'print-three-values',
  'use-variables',
  'variable-assignment',
  'operators',
  'string-concatenation',
  'kroner-to-euro',
  'functions',
  'your-semester-in-ects',
  'is-it-daytime',
  'big-or-small',
  'even-or-odd',
  'time-of-day',
  'multiplication-series',
  'guess-the-number',
  'nim-bonus',
  'while-loop-quiz-1',
  'while-loop-quiz-2',
  'while-loop-quiz-3',
  'while-loop-quiz-4',
  'while-loop-quiz-5',
  'while-loop-quiz-6',
  'for-loop-quiz-1',
  'for-loop-quiz-2',
  'for-loop-quiz-3',
  'for-loop-quiz-4',
  'for-loop-quiz-5',
  'for-loop-quiz-6',
  'person-class',
  'flight-ticket-class',
  'container-class',
  'build-a-tree',
  'grandpas-time-machine',
  'grandmas-blackmarket-kitchen'
);

-- ─────────────────────────── set memberships ───────────────────────────
-- Rebuilt from scratch every run. Canonical order matches the frontend's
-- ASSIGNMENTS array index; per-day sets are slices, re-numbered from 0.
--   Day 1: indices 0–8   (9)   Day 2: 9–27 (19)   Day 3: 28–33 (6)   total 34

WITH ordered(slug, ord) AS (VALUES
  ('hello-itu', 0),
  ('print-three-values', 1),
  ('use-variables', 2),
  ('variable-assignment', 3),
  ('operators', 4),
  ('string-concatenation', 5),
  ('kroner-to-euro', 6),
  ('functions', 7),
  ('your-semester-in-ects', 8),
  ('is-it-daytime', 9),
  ('big-or-small', 10),
  ('even-or-odd', 11),
  ('time-of-day', 12),
  ('multiplication-series', 13),
  ('guess-the-number', 14),
  ('nim-bonus', 15),
  ('while-loop-quiz-1', 16),
  ('while-loop-quiz-2', 17),
  ('while-loop-quiz-3', 18),
  ('while-loop-quiz-4', 19),
  ('while-loop-quiz-5', 20),
  ('while-loop-quiz-6', 21),
  ('for-loop-quiz-1', 22),
  ('for-loop-quiz-2', 23),
  ('for-loop-quiz-3', 24),
  ('for-loop-quiz-4', 25),
  ('for-loop-quiz-5', 26),
  ('for-loop-quiz-6', 27),
  ('person-class', 28),
  ('flight-ticket-class', 29),
  ('container-class', 30),
  ('build-a-tree', 31),
  ('grandpas-time-machine', 32),
  ('grandmas-blackmarket-kitchen', 33)
),
resolved AS (
  SELECT t.id AS assignment_id, o.ord
  FROM ordered o
  JOIN assignment t ON t.slug = o.slug
)
INSERT INTO assignment_set_assignment (assignment_set_id, assignment_id, order_index)
SELECT 'day1-2026', assignment_id, ord        FROM resolved WHERE ord BETWEEN 0 AND 8
UNION ALL
SELECT 'day2-2026', assignment_id, ord - 9    FROM resolved WHERE ord BETWEEN 9 AND 27
UNION ALL
SELECT 'day3-2026', assignment_id, ord - 28   FROM resolved WHERE ord BETWEEN 28 AND 33
UNION ALL
SELECT 'all-assignments-for-solo-2026', assignment_id, ord FROM resolved;

DO $check$
DECLARE n int;
BEGIN
  SELECT count(*) INTO n FROM assignment_set_assignment WHERE assignment_set_id = 'all-assignments-for-solo-2026';
  IF n <> 34 THEN
    RAISE EXCEPTION 'seed error: expected 34 assignments in all-assignments-for-solo-2026, got % (typo in a slug?)', n;
  END IF;
END
$check$;

COMMIT;
