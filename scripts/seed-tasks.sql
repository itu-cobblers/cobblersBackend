-- ============================================================================
-- seed-tasks.sql — BootIT assignments + assignment sets (Day 1–3).
-- Content sourced from BootIT DAY 1.md / DAY 2.md (2026 rewrite) + existing Day 3.
--
-- Usage (schema must already exist via `dotnet ef database update`):
--
--     psql "$CONNECTION_STRING" -f scripts/seed-tasks.sql
--     (use a postgresql:// URI or PG* env vars — not the Npgsql Host=...; form)
--
-- Full reset each run:
--   * TRUNCATE assignment-related tables with RESTART IDENTITY so ids start at 1.
--   * Then INSERT all rows fresh (including sample_solution_json).
--   * Does NOT wipe students / sessions / attendance.
--
-- Conventions (see SCHEMA.md):
--   * assignment.id is DB-assigned — never written here. References use slug.
--   * kind is lowercase text: 'code' | 'predict' | 'project'.
--   * Day 1–2: lesson_json, content_json, sample_solution_json, grading_json
--     are always populated (hint may be NULL).
--   * Predict grading_json: { "predict": { "compare", "expectedOutput", "accept"? } }
--     — graded by AssignmentGrader on submit (no Piston run).
--
-- Counts: Day 1 = 9, Day 2 = 24, Day 3 = 6, total = 39.
-- ============================================================================

BEGIN;

-- Wipe seed tables and restart identity sequences (assignment.id back to 1).
TRUNCATE TABLE
  submission,
  assignment_set_assignment,
  assignment,
  assignment_set
RESTART IDENTITY CASCADE;

-- ────────────────────────────── assignment sets ────────────────────────────

INSERT INTO assignment_set (assignment_set_id, display_title) VALUES
  ('day1-2026',                     'BootIT Day 1 — 2026'),
  ('day2-2026',                     'BootIT Day 2 — 2026'),
  ('day3-2026',                     'BootIT Day 3 — 2026'),
  ('all-assignments-for-solo-2026', 'BootIT — All Tasks (Solo) 2026');

-- ──────────────────────────────── assignments ──────────────────────────────

INSERT INTO assignment (slug, kind, title, description, hint, lesson_json, content_json, sample_solution_json, grading_json) VALUES

-- ─────────────────────────── DAY 1 — basics ───────────────────────────
(
  'hello-itu', 'code', 'Hello ITU',
  $txt$Now it is your turn: print a sentence to say hello to your new university. Print exactly: Hello ITU!$txt$,
  $txt$System.out.println("Hello ITU!");$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Printing a message is the most basic thing every programming language can do. In Java it takes a class, a main method, and one print statement:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$public class Main {
    public static void main(String[] args) {
        System.out.println("Hello World!");
    }
}$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        // Say hello to ITU
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        System.out.println("Hello ITU!");
    }
}
$java$::text),
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
    jsonb_build_object('kind', 'code', 'code', $txt$System.out.println("Hello World!");
System.out.println(42);
System.out.println(3.14);$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        // 1) a greeting  2) your birth year  3) years in Copenhagen (with a decimal)
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        System.out.println("Hello, my name is Aiting!");
        System.out.println(1996);
        System.out.println(1.95);
    }
}
$java$::text),
  $j${"all": [{"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+$"}, {"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+\\.\\d+$"}, {"target": "stdout", "op": "regex", "pattern": "[A-Za-z]{2,}"}]}$j$::jsonb
),
(
  'use-variables', 'code', 'Use variables',
  $txt$Print the same three values as before, but this time store each one in a variable first, then print the variable. Pick the right type: String for the greeting, int for the year, double for the years in Copenhagen.$txt$,
  $txt$String greeting = "Hello, my name is …!"; then System.out.println(greeting);$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$The same values can be stored in variables first. A variable has a type, a name, and a value:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$int x = 42;
System.out.println(x);

String s = "hi";
System.out.println(s);

double d = 3.14;
System.out.println(d);

boolean b = true;
System.out.println(b);$txt$),
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
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        String greeting = "Hello, my name is Aiting!";
        int birthYear = 1996;
        double yearsInCopenhagen = 1.95;

        System.out.println(greeting);
        System.out.println(birthYear);
        System.out.println(yearsInCopenhagen);
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "\\bString\\s+\\w+\\s*="}, {"target": "code", "op": "regex", "pattern": "\\bint\\s+\\w+\\s*="}, {"target": "code", "op": "regex", "pattern": "\\bdouble\\s+\\w+\\s*="}, {"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+$"}, {"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+\\.\\d+$"}]}$j$::jsonb
),
(
  'variable-assignment', 'code', 'Variable assignment',
  $txt$Use one int variable called age (starting at 27) to print "ITU is 27 years old." Then update the SAME variable with age = age + 1 and use it again to print "Next year ITU will be 28 years old."$txt$,
  $txt$Build each sentence with String +, or use print/println.$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$A variable can be given a new value later — that is why it is called a variable. The same variable then prints a different value:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$int year = 2026;
System.out.print("The year is ");
System.out.println(year);   // The year is 2026

year = year + 1;
System.out.print("The year is now ");
System.out.println(year);   // The year is now 2027$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$Fun fact: ITU is the youngest university in Denmark, founded in 1999 — it turns 27 in 2026.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int age = 27;
        // Print both sentences — update age in between
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        int age = 27;
        System.out.println("ITU is " + age + " years old.");

        age = age + 1;
        System.out.println("Next year ITU will be " + age + " years old.");
    }
}
$java$::text),
  $j${"all": [{"target": "stdout", "op": "contains", "value": "ITU is 27 years old."}, {"target": "stdout", "op": "contains", "value": "Next year ITU will be 28 years old."}, {"target": "code", "op": "regex", "pattern": "\\bage\\s*=\\s*age\\s*\\+\\s*1\\b|\\bage\\s*\\+\\+|\\bage\\s*\\+=\\s*1\\b"}]}$j$::jsonb
),
(
  'operators', 'code', 'Operators',
  $txt$Fun facts from ITU Key figures 2025: about 23.4% of students are international. The Master of Software Design intake is around 130 students in recent years.

Print how many of those ~130 Soft Design masters students are international (approximately): 130 * 23.4 / 100.0 → 30.42

Use * and / in your code.$txt$,
  $txt$Use a double for 23.4 and divide by 100.0 so you keep the decimals.$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Java can calculate with + (plus), - (minus), * (multiply) and / (divide):$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$System.out.println(3 + 3);   // 6

int x = 2;
System.out.println(x * x);   // 4

int y = 6;
System.out.println(y / 3);   // 2$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$Dividing two ints throws the decimals away. Use a double (e.g. 100.0 or 23.4) when you want a fractional result.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int mastersStudents = 130;
        double internationalPercent = 23.4;
        // Print international count
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        int mastersStudents = 130;
        double internationalPercent = 23.4;

        double international = mastersStudents * internationalPercent / 100.0;
        System.out.println(international);
    }
}
$java$::text),
  $j${"all": [{"target": "stdout", "op": "contains", "value": "30.42"}, {"target": "code", "op": "contains", "value": "*"}, {"target": "code", "op": "contains", "value": "/"}]}$j$::jsonb
),
(
  'string-concatenation', 'code', 'String concatenation',
  $txt$The starter prints "Hello my friend!". Ask the person sitting next to you for their name, then modify the code to greet them personally: Hello my friend, {Name}!$txt$,
  $txt$Add a String name = "…"; and concatenate it after "friend, ".$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$+ between Strings glues them together — this is called concatenation:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$String hi = "Hello ";
String world = "World!";
String greet = hi + world;

System.out.println(greet);   // Hello World!$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$It also works between Strings and numbers:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$int year = 2026;
System.out.println("The year is " + year);$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        String first = "Hello";
        String second = "my";
        String third = "friend";
        System.out.println(first + " " + second + " " + third + "!");
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        String first = "Hello";
        String second = "my";
        String third = "friend";
        String name = "Aiting";
        System.out.println(first + " " + second + " " + third + ", " + name + "!");
    }
}
$java$::text),
  $j${"all": [{"target": "stdout", "op": "regex", "pattern": "Hello my friend, .+!"}, {"target": "code", "op": "contains", "value": "+"}]}$j$::jsonb
),
(
  'kroner-to-euro', 'code', 'Kroner to euro',
  $txt$Modify the code so it converts the opposite way: from kroner to euro. For a 20 dkk coffee, print: "20 dkk corresponds to 2.6845637583892616 euro." (All the decimals are fine — that is just how doubles print.)$txt$,
  $txt$Divide instead of multiply: dkk / 7.45$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$During the break you meet a friend at ITU's own café, Cafe Analog. A coffee costs 20 dkk. Your friend says that is cheap — but you want to see it in euro. This code converts the other way, from euro to kroner:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$public class Main {
    public static void main(String[] args) {
        int eur = 100;
        double dkk = eur * 7.45;
        System.out.println(eur + " euro corresponds to " + dkk + " kr.");
    }
}$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int eur = 100;
        double dkk = eur * 7.45;
        System.out.println(eur + " euro corresponds to " + dkk + " kr.");
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        int dkk = 20;
        double eur = dkk / 7.45;
        System.out.println(dkk + " dkk corresponds to " + eur + " euro.");
    }
}
$java$::text),
  $j${"all": [{"target": "stdout", "op": "contains", "value": "20"}, {"target": "stdout", "op": "contains", "value": "corresponds to"}, {"target": "stdout", "op": "contains", "value": "euro"}, {"target": "stdout", "op": "regex", "pattern": "2\\.68"}, {"target": "code", "op": "regex", "pattern": "/\\s*7\\.45"}]}$j$::jsonb
),
(
  'functions', 'code', 'Functions',
  $txt$The no-parameter version always converts 100 kr. Rewrite it so dkk2eur takes one parameter — the amount in dkk — and prints "{dkk} kr corresponds to {eur} euro". Call it twice from main: once with 20 (your Analog coffee) and once with another price, e.g. 15.$txt$,
  $txt$static void dkk2eur(double dkk) { … }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Functions let us reuse code and give a snippet a clear responsibility. This does exactly the same as the previous exercise, wrapped in a function:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$public class Main {
    static void dkk2eur() {
        double dkk = 100;
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    public static void main(String[] args) {
        dkk2eur();
    }
}$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$But this function always converts 100 kr. With a parameter, the same function works for any value:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$static void dkk2eur(double dkk) {
    double eur = dkk / 7.45;
    System.out.println(dkk + " kr corresponds to " + eur + " euro");
}

public static void main(String[] args) {
    dkk2eur(100);
    dkk2eur(20);
}$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {

    // Change this to take one parameter: static void dkk2eur(double dkk)
    static void dkk2eur() {
        double dkk = 100;
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    public static void main(String[] args) {
        // Call dkk2eur twice with different prices
        dkk2eur();
    }
}
$java$
  ),
  to_jsonb($java$public class Main {

    static void dkk2eur(double dkk) {
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    public static void main(String[] args) {
        dkk2eur(20);
        dkk2eur(15);
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "static\\s+void\\s+\\w+\\s*\\(\\s*(?:int|double)\\s+\\w+\\s*\\)"}, {"target": "stdout", "op": "regex", "pattern": "corresponds to", "flags": "i"}, {"target": "stdout", "op": "contains", "value": "euro"}]}$j$::jsonb
),
(
  'your-semester-in-ects', 'code', 'Your semester in ECTS',
  $txt$Write a function printCourse(String name, double ects, int semester) that prints "{name} ({ects} ECTS) is in semester {semester}."

You are starting Software Design. Call it from main for all three semester-1 courses:
- Introductory Programming (15 ECTS)
- Discrete Mathematics (7.5 ECTS)
- Software Engineering (7.5 ECTS)
all in semester 1.$txt$,
  $txt$Three parameters: static void printCourse(String name, double ects, int semester)$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$At ITU every course is worth ECTS points, and a full semester adds up to 30 ECTS. A function with several parameters can print any course the same way. Software Design semester 1: Introductory Programming (15), Discrete Mathematics (7.5), Software Engineering (7.5).$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {

    // Declare printCourse here

    public static void main(String[] args) {
        // Print all three Soft Design semester-1 courses
    }
}
$java$
  ),
  to_jsonb($java$public class Main {

    static void printCourse(String name, double ects, int semester) {
        System.out.println(name + " (" + ects + " ECTS) is in semester " + semester + ".");
    }

    public static void main(String[] args) {
        printCourse("Introductory Programming", 15, 1);
        printCourse("Discrete Mathematics", 7.5, 1);
        printCourse("Software Engineering", 7.5, 1);
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "static\\s+void\\s+\\w+\\s*\\(\\s*String\\s+\\w+\\s*,\\s*(?:int|double)\\s+\\w+\\s*,\\s*int\\s+\\w+\\s*\\)"}, {"target": "stdout", "op": "regex", "pattern": ".+ \\(.+ ECTS\\) is in semester \\d+\\."}, {"target": "stdout", "op": "contains", "value": "Introductory Programming"}, {"target": "stdout", "op": "contains", "value": "Discrete Mathematics"}, {"target": "stdout", "op": "contains", "value": "Software Engineering"}]}$j$::jsonb
),

-- ─────────────────────────── DAY 2 — conditionals, loops, input ───────────────────────────
(
  'at-itu-welcome', 'code', 'Welcome to ITU',
  $txt$You just walked into the ITU atrium. You have a boolean atItu. Print "Welcome to ITU!" only if atItu is true. If you set atItu to false, the program should print nothing.$txt$,
  $txt$if (atItu) { System.out.println("Welcome to ITU!"); }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$An if runs a block of code only when a condition is true. If the condition is false, Java simply skips the block and continues.$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$if (condition) {
    System.out.println("Yes the condition is correct");
}$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        boolean atItu = true;
        // Print Welcome to ITU! only if atItu is true
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        boolean atItu = true;
        if (atItu) {
            System.out.println("Welcome to ITU!");
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "\\bif\\s*\\("}, {"target": "stdout", "op": "containsLine", "value": "Welcome to ITU!"}]}$j$::jsonb
),
(
  'scrollbar-friday', 'code', 'Scrollbar Friday',
  $txt$Fun fact: Scrollbar is the Friday bar at ITU — open every Friday after 15:00 during the semester.

Write a program that prints whether it is Scrollbar day. One clear true case: Friday.

Set the day yourself with a String (no live calendar — BootIT is on Thursday, so you can flip the value to test both branches):

- If weekday == "Friday" → Yes, it is Friday, Scrollbar will open today!
- Otherwise → No, Scrollbar is closed.

Try both "Friday" and "Thursday".$txt$,
  $txt$if (weekday == "Friday") { ... } else { ... }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$With if / else, exactly one of the two branches runs — to be, or not to be:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$// code that is always executed

if (condition) {
    System.out.println("To be");
} else {
    System.out.println("Not to be");
}

// code that is always executed$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        String weekday = "Friday"; // try "Thursday" too
        // Print Yes... or No...
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        String weekday = "Friday"; // try "Thursday" too
        if (weekday == "Friday") {
            System.out.println("Yes, it is Friday, Scrollbar will open today!");
        } else {
            System.out.println("No, Scrollbar is closed.");
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "==\\s*\"Friday\""}, {"target": "stdout", "op": "regex", "pattern": "Yes|No", "flags": "i"}]}$j$::jsonb
),
(
  'is-friday-boolean', 'code', 'Is Friday? (boolean)',
  $txt$Rewrite the Scrollbar Friday check from the previous assignment using a boolean variable.

1. Keep a settable String weekday (e.g. "Friday" or "Thursday").
2. Create boolean isFriday = (weekday == "Friday");
3. Use if (isFriday) { ... } else { ... } (do not write if (isFriday == true)).
4. Keep the same two print messages.$txt$,
  $txt$boolean isFriday = (weekday == "Friday"); then if (isFriday) ...$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$A boolean variable can take only two values: true and false.$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$String weekday = "Thursday"; // BootIT day
boolean isThursday = (weekday == "Thursday");

// Idiomatic Java: use the boolean directly
if (isThursday) {
    System.out.println("It is Thursday");
}$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        String weekday = "Friday"; // try "Thursday" too
        // boolean isFriday = ...
        // if (isFriday) ...
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        String weekday = "Friday"; // try "Thursday" too
        boolean isFriday = (weekday == "Friday");

        if (isFriday) {
            System.out.println("Yes, it is Friday, Scrollbar will open today!");
        } else {
            System.out.println("No, Scrollbar is closed.");
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "\\bboolean\\s+\\w+\\s*="}, {"target": "code", "op": "regex", "pattern": "if\\s*\\(\\s*\\w+\\s*\\)"}, {"target": "stdout", "op": "regex", "pattern": "Yes|No", "flags": "i"}]}$j$::jsonb
),
(
  'canteen-lunch', 'code', 'Canteen lunch hours',
  $txt$The ITU canteen serves lunch Monday–Friday, 11:00–14:00. After 13:45 there is a late-lunch discount.

Assume it is already a weekday — check the clock with nested conditions.

Use a double for the time of day (e.g. 11.0 = 11:00, 13.75 = 13:45).

- time < 11.0 → Too early — lunch starts at 11:00.
- time >= 14.0 → Too late — lunch ended at 14:00.
- otherwise:
  - print Lunch is being served.
  - then if time >= 13.75 → Late lunch discount applies! else → Full price.$txt$,
  $txt$Nest if/else. Try time = 13.75, 10.5, 12.0, 14.5.$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$You can put an if inside another if. The inner block only runs when the outer condition is also true.$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$int number = 42;

if (number > 0) {
    if (number > 100) {
        System.out.println("positive and big");
    } else {
        System.out.println("positive but small");
    }
} else {
    System.out.println("not positive");
}$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        double time = 13.75; // try 10.5, 12.0, 13.5, 14.5
        // Nested if/else for early / late / serving + discount
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        double time = 13.75; // try 10.5, 12.0, 13.5, 14.5

        if (time < 11.0) {
            System.out.println("Too early — lunch starts at 11:00.");
        } else {
            if (time >= 14.0) {
                System.out.println("Too late — lunch ended at 14:00.");
            } else {
                System.out.println("Lunch is being served.");
                if (time >= 13.75) {
                    System.out.println("Late lunch discount applies!");
                } else {
                    System.out.println("Full price.");
                }
            }
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "contains", "value": "if"}, {"target": "stdout", "op": "contains", "value": "Lunch is being served."}, {"target": "stdout", "op": "contains", "value": "Late lunch discount applies!"}]}$j$::jsonb
),
(
  'fitness-access', 'code', 'Fitness access',
  $txt$The ITU fitness room (basement, beneath Scrollbar) needs a membership — but on the first Tuesday of every month there is free trial access.

You may enter if you have a membership OR it is free-trial Tuesday:

if (hasMembership || isFreeTrialTuesday) print "Accessed" else print "Not allowed".

Flip the two booleans and check both outcomes.$txt$,
  $txt$if (hasMembership || isFreeTrialTuesday) { ... }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Boolean comparisons: == != < > <= >=
Logical operators: ! (not), && (and), || (or) — at least one must be true for ||.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        boolean hasMembership = false;
        boolean isFreeTrialTuesday = true; // first Tuesday of the month

        // if either is true → Accessed, else → Not allowed
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        boolean hasMembership = false;
        boolean isFreeTrialTuesday = true;

        if (hasMembership || isFreeTrialTuesday) {
            System.out.println("Accessed");
        } else {
            System.out.println("Not allowed");
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "contains", "value": "||"}, {"target": "stdout", "op": "regex", "pattern": "Accessed|Not allowed"}]}$j$::jsonb
),
(
  'student-day-place', 'code', 'A common day as an ITU student',
  $txt$Write a function place(String period) that returns where a student probably is:

- morning → lectures, exercises, or a group room
- break → the Cafe Analog coffee queue
- noon → the canteen rush
- afternoon → the outdoor grass (if the sun is out)

From main, set a period and print:
System.out.println("During " + period + ", an ITU student is probably at " + place(period));$txt$,
  $txt$static String place(String period) { return ...; }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Previous functions were mostly void (they printed). A function can also return a value to the caller with return. The return type goes where void used to be (String, int, boolean, …).$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        String period = "morning"; // try "break", "noon", "afternoon"
        System.out.println("During " + period + ", an ITU student is probably at " + place(period));
    }

    static String place(String period) {
        // return the matching place
        return "";
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        String period = "morning";
        System.out.println("During " + period + ", an ITU student is probably at " + place(period));
    }

    static String place(String period) {
        if (period == "morning") {
            return "lectures, exercises, or a group room";
        } else if (period == "break") {
            return "the Cafe Analog coffee queue";
        } else if (period == "noon") {
            return "the canteen rush";
        } else {
            return "the outdoor grass (if the sun is out)";
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "static\\s+String\\s+\\w+\\s*\\(\\s*String\\s+\\w+\\s*\\)"}, {"target": "code", "op": "contains", "value": "return"}, {"target": "stdout", "op": "regex", "pattern": "During .+, an ITU student is probably at .+"}]}$j$::jsonb
),
(
  'while-five-lines', 'code', 'While loop — five lines',
  $txt$Loops turn repetition into initialize → condition → body → increment.

Start from the starter. Right now it runs while i < 10 (i goes 0 … 9). Change only the loop condition so the program prints exactly 5 lines.

Hint: which number should replace 10, and why?$txt$,
  $txt$while (i < 5) runs for i = 0,1,2,3,4 — exactly 5 times.$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Without a loop you copy-paste. With while:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$int i = 0;                 // initialization
while (i < 10) {           // loop condition
    System.out.println("I will push my code before the deadline!");
    i = i + 1;             // increment
}$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$i starts at 0, condition i < 10 → body runs for 0..9 (10 times). It never runs with i == 10.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {

    static void function(int number) {
        System.out.println("Line " + number + ": I will push my code before the deadline!");
    }

    public static void main(String[] args) {
        int i = 0; // initialization
        while (i < 10) { // loop condition — change this
            function(i);
            i = i + 1; // increment
        }
    }
}
$java$
  ),
  to_jsonb($java$public class Main {

    static void function(int number) {
        System.out.println("Line " + number + ": I will push my code before the deadline!");
    }

    public static void main(String[] args) {
        int i = 0;
        while (i < 5) { // 0,1,2,3,4 → exactly 5 lines
            function(i);
            i = i + 1;
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "contains", "value": "while"}, {"target": "stdout", "op": "contains", "value": "Line 0:"}, {"target": "stdout", "op": "contains", "value": "Line 4:"}, {"not": {"target": "stdout", "op": "contains", "value": "Line 5:"}}]}$j$::jsonb
),
(
  'while-loop-quiz-1', 'predict', 'While Loop Quiz 1',
  $txt$Read the loop and predict exactly what it prints. Type your answer in the output window; use return/enter for each println line.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
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
1$txt$
  ),
  to_jsonb($txt$10
9
8
7
6
5
4
3
2
1$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "10\n9\n8\n7\n6\n5\n4\n3\n2\n1"}}$j$::jsonb
),
(
  'while-loop-quiz-2', 'predict', 'While Loop Quiz 2',
  $txt$Read the loop and predict exactly what it prints. Type your answer in the output window; use return/enter for each println line.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
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
9$txt$
  ),
  to_jsonb($txt$1
3
5
7
9$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "1\n3\n5\n7\n9"}}$j$::jsonb
),
(
  'while-loop-quiz-3', 'predict', 'While Loop Quiz 3',
  $txt$Read the loop and predict exactly what it prints. Type your answer in the output window; use return/enter for each println line.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
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
64$txt$
  ),
  to_jsonb($txt$1
2
4
8
16
32
64$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "1\n2\n4\n8\n16\n32\n64"}}$j$::jsonb
),
(
  'while-loop-quiz-4', 'predict', 'While Loop Quiz 4',
  $txt$Careful with this one! Predict what it prints — some loops never stop.$txt$,
  $txt$What is 1 × 1? Does i ever change?$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
  jsonb_build_object(
    'snippet', $java$int i = 1;
while (i < 42) {
    System.out.println(i);
    i = i * i;
}$java$,
    'expectedOutput', $txt$infinite loop$txt$,
    'accept', jsonb_build_array($txt$infinite$txt$, $txt$never stops$txt$, $txt$never ends$txt$, $txt$forever$txt$, $txt$loops forever$txt$, $txt$does not stop$txt$, $txt$doesn't stop$txt$)
  ),
  to_jsonb($txt$infinite loop$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "infinite loop", "accept": ["infinite", "never stops", "never ends", "forever", "loops forever", "does not stop", "doesn't stop"]}}$j$::jsonb
),
(
  'while-loop-quiz-5', 'predict', 'While Loop Quiz 5',
  $txt$Read the loop and predict exactly what it prints. Type your answer in the output window; use return/enter for each println line.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
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
15$txt$
  ),
  to_jsonb($txt$0
3
6
9
12
15$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "0\n3\n6\n9\n12\n15"}}$j$::jsonb
),
(
  'while-loop-quiz-6', 'predict', 'While Loop Quiz 6',
  $txt$Read the loop and predict exactly what it prints. Type your answer in the output window; use return/enter for each println line.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
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
2$txt$
  ),
  to_jsonb($txt$64
32
16
8
4
2$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "64\n32\n16\n8\n4\n2"}}$j$::jsonb
),
(
  'analog-tickets-while', 'code', 'Analog tickets (while)',
  $txt$Cafe Analog has a mobile app where you can buy 5 tickets at once with a discount. You are a heavy coffee drinker and want 50 tickets in one go — so you need a loop that buys a pack of 5 tickets, ten times.

Using a while loop, print the running total after each pack: 5, 10, 15, …, 50 (one number per line).$txt$,
  $txt$Accumulate total += ticketsPerPack inside while (packs < 10).$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Use a while loop when you know you need to repeat until a counter reaches a limit. Initialize → check → body → increment.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int ticketsPerPack = 5;
        // Buy 10 packs with a while loop; print the running total each time
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        int ticketsPerPack = 5;
        int total = 0;
        int packs = 0;
        while (packs < 10) {
            total = total + ticketsPerPack;
            System.out.println(total);
            packs = packs + 1;
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "contains", "value": "while"}, {"target": "stdout", "op": "containsLine", "value": "50"}, {"target": "stdout", "op": "containsLine", "value": "45"}]}$j$::jsonb
),
(
  'analog-tickets-for', 'code', 'Analog tickets (for)',
  $txt$Rewrite the Cafe Analog tickets program. Same output (5 … 50), but use a for loop instead of while. The starter is the while solution — change it to for.$txt$,
  $txt$for (int packs = 0; packs < 10; packs = packs + 1) { ... }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$The for loop packs initialization, condition, and increment into one header — nicer when you already know how many times to repeat:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$for (int i = 0; i < 10; i = i + 1) {
    System.out.println(i);
}$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$A while loop and a for loop can do the same job.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int ticketsPerPack = 5;
        int total = 0;
        int packs = 0;
        while (packs < 10) {
            total = total + ticketsPerPack;
            System.out.println(total);
            packs = packs + 1;
        }
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        int ticketsPerPack = 5;
        int total = 0;
        for (int packs = 0; packs < 10; packs = packs + 1) {
            total = total + ticketsPerPack;
            System.out.println(total);
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "\\bfor\\s*\\("}, {"not": {"target": "code", "op": "regex", "pattern": "\\bwhile\\s*\\("}}, {"target": "stdout", "op": "containsLine", "value": "50"}, {"target": "stdout", "op": "containsLine", "value": "45"}]}$j$::jsonb
),
(
  'for-loop-quiz-1', 'predict', 'For Loop Quiz 1',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
  jsonb_build_object(
    'snippet', $java$for (int i = 10; i > 0; i = i - 2) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$10
8
6
4
2$txt$
  ),
  to_jsonb($txt$10
8
6
4
2$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "10\n8\n6\n4\n2"}}$j$::jsonb
),
(
  'for-loop-quiz-2', 'predict', 'For Loop Quiz 2',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
  jsonb_build_object(
    'snippet', $java$for (int i = 1; i < 10; i = i + 3) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$1
4
7$txt$
  ),
  to_jsonb($txt$1
4
7$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "1\n4\n7"}}$j$::jsonb
),
(
  'for-loop-quiz-3', 'predict', 'For Loop Quiz 3',
  $txt$Careful with this one! Predict what it prints — some loops never stop.$txt$,
  $txt$What is 1 × 1? Does i ever grow?$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
  jsonb_build_object(
    'snippet', $java$for (int i = 1; i < 10; i = i * i) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$infinite loop$txt$,
    'accept', jsonb_build_array($txt$infinite$txt$, $txt$never stops$txt$, $txt$never ends$txt$, $txt$forever$txt$, $txt$loops forever$txt$, $txt$does not stop$txt$, $txt$doesn't stop$txt$)
  ),
  to_jsonb($txt$infinite loop$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "infinite loop", "accept": ["infinite", "never stops", "never ends", "forever", "loops forever", "does not stop", "doesn't stop"]}}$j$::jsonb
),
(
  'for-loop-quiz-4', 'predict', 'For Loop Quiz 4',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
  jsonb_build_object(
    'snippet', $java$for (int i = 0; i <= 15; i = i + 3) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$0
3
6
9
12
15$txt$
  ),
  to_jsonb($txt$0
3
6
9
12
15$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "0\n3\n6\n9\n12\n15"}}$j$::jsonb
),
(
  'for-loop-quiz-5', 'predict', 'For Loop Quiz 5',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
  jsonb_build_object(
    'snippet', $java$for (int i = 1; i <= 10000; i = i * 10) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$1
10
100
1000
10000$txt$
  ),
  to_jsonb($txt$1
10
100
1000
10000$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "1\n10\n100\n1000\n10000"}}$j$::jsonb
),
(
  'for-loop-quiz-6', 'predict', 'For Loop Quiz 6',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Predict the exact output of the snippet. Type your answer in the output box. Use a new line for each System.out.println (press Enter / return between lines). If the loop never stops, answer: infinite loop.$txt$)
  ),
  jsonb_build_object(
    'snippet', $java$for (int i = 64; i >= 2; i = i / 2) {
    System.out.println(i);
}$java$,
    'expectedOutput', $txt$64
32
16
8
4
2$txt$
  ),
  to_jsonb($txt$64
32
16
8
4
2$txt$::text),
  $j${"predict": {"compare": "normalized", "expectedOutput": "64\n32\n16\n8\n4\n2"}}$j$::jsonb
),
(
  'gym-workout', 'code', 'Gym workout (nested for)',
  $txt$You are in the ITU fitness room. Today's plan: 4 sets, 12 reps each. Use a nested for to log every rep — outer loop = set (1…4), inner loop = rep (1…12):

Set 1 Rep 1
…
Set 4 Rep 12$txt$,
  $txt$for (int set = 1; set <= 4; set++) { for (int rep = 1; rep <= 12; rep++) { ... } }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Sometimes you need two counters. A nested for reads like the shape of the data:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$for (int set = 1; set <= 2; set = set + 1) {
    for (int rep = 1; rep <= 3; rep = rep + 1) {
        System.out.println("Set " + set + " Rep " + rep);
    }
}$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        // nested for: sets 1..4, reps 1..12
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        for (int set = 1; set <= 4; set = set + 1) {
            for (int rep = 1; rep <= 12; rep = rep + 1) {
                System.out.println("Set " + set + " Rep " + rep);
            }
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "\\bfor\\s*\\("}, {"target": "stdout", "op": "containsLine", "value": "Set 1 Rep 1"}, {"target": "stdout", "op": "containsLine", "value": "Set 4 Rep 12"}]}$j$::jsonb
),
(
  'analog-reusable-cup-stamps', 'code', 'Help Analog go sustainable',
  $txt$Cafe Analog wants fewer disposable cups. Every time you buy a drink and bring your own cup, you get a stamp on your card — the 10th stamp is a free cup, and you start a fresh card right after.

Simulate 24 drinks bought (one per loop iteration) with a for or while loop: stamps starts at 0, and each drink adds one stamp.
- While stamps != 10: print "Brought my own cup, got a stamp! ({stamps}/10)".
- The moment stamps == 10: print "Free cup! Here's a new stamp card." instead, then reset stamps back to 0.$txt$,
  $txt$if (stamps != 10) { ... } else { System.out.println("Free cup! Here's a new stamp card."); stamps = 0; }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Nothing new here — just a loop combined with if/else, the same way you did for the canteen and fitness room. The trick is that the stamp counter resets itself once it is spent.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        int drinksBought = 24;
        int stamps = 0;

        // Loop drinksBought times. Each drink: stamps++.
        // stamps != 10  -> print a stamp message
        // stamps == 10  -> print a free-cup message, then reset stamps to 0
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        int drinksBought = 24;
        int stamps = 0;

        for (int drink = 1; drink <= drinksBought; drink = drink + 1) {
            stamps = stamps + 1;

            if (stamps != 10) {
                System.out.println("Brought my own cup, got a stamp! (" + stamps + "/10)");
            } else {
                System.out.println("Free cup! Here's a new stamp card.");
                stamps = 0;
            }
        }
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "\\bif\\s*\\("}, {"target": "code", "op": "regex", "pattern": "\\bfor\\s*\\(|\\bwhile\\s*\\("}, {"target": "stdout", "op": "containsLine", "value": "Free cup! Here's a new stamp card."}, {"target": "stdout", "op": "containsLine", "value": "Brought my own cup, got a stamp! (4/10)"}]}$j$::jsonb
),
(
  'beerpong-at-scrollbar', 'code', 'Beer pong at Scrollbar',
  $txt$Friday at Scrollbar (ITU's Friday bar) means one thing: beer pong. Set up the rack, then simulate a full round.

1) Print the triangle rack with a nested for loop — one line per row: "Row 1: O", "Row 2: O O", "Row 3: O O O", "Row 4: O O O O" (10 cups total).
2) Simulate throws with a while loop. cupsLeft starts at 10. Each throw, roll with Random rng = new Random(): int roll = rng.nextInt(4) gives 0, 1, 2 or 3, each equally likely — treat roll == 0 as a hit (25% chance). On a hit, remove a cup and print "Throw {n}: SPLASH! {cupsLeft} cups left."; otherwise print "Throw {n}: MISS! {cupsLeft} cups left." Keep throwing until cupsLeft is 0, then print "GAME OVER — the rack is empty. Chug up!"$txt$,
  $txt$Random rng = new Random(); int roll = rng.nextInt(4); if (roll == 0) { ... hit ... } else { ... miss ... }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$One new tool: java.util.Random gives pseudo-random numbers. Calling nextInt(4) produces 0, 1, 2, or 3; use one of those values to decide whether the throw is a hit or miss.$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$import java.util.Random;

Random rng = new Random();
int roll = rng.nextInt(4);     // 0, 1, 2, or 3 -- each 25% likely
if (roll == 0) {
    System.out.println("Hit!");
}$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$Everything else — the while loop, if/else, and the cupsLeft counter — is exactly what you already know.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$import java.util.Random;

public class Main {
    public static void main(String[] args) {
        // 1) Print the rack: 4 rows, "Row 1: O" ... "Row 4: O O O O"

        // 2) Simulate throws until the rack (10 cups) is empty.
        //    Random rng = new Random();
        //    Each throw: int roll = rng.nextInt(4); roll == 0 -> hit (25% chance)
    }
}
$java$
  ),
  to_jsonb($java$import java.util.Random;

public class Main {
    public static void main(String[] args) {
        for (int row = 1; row <= 4; row = row + 1) {
            System.out.print("Row " + row + ":");
            for (int cup = 1; cup <= row; cup = cup + 1) {
                System.out.print(" O");
            }
            System.out.println();
        }

        Random rng = new Random();
        int cupsLeft = 10;
        int throwNumber = 0;

        while (cupsLeft > 0) {
            throwNumber = throwNumber + 1;
            int roll = rng.nextInt(4);
            if (roll == 0) {
                cupsLeft = cupsLeft - 1;
                System.out.println("Throw " + throwNumber + ": SPLASH! " + cupsLeft + " cups left.");
            } else {
                System.out.println("Throw " + throwNumber + ": MISS! " + cupsLeft + " cups left.");
            }
        }

        System.out.println("GAME OVER — the rack is empty. Chug up!");
    }
}
$java$::text),
  $j${"all": [{"target": "code", "op": "regex", "pattern": "\\bfor\\s*\\("}, {"target": "code", "op": "regex", "pattern": "\\bwhile\\s*\\("}, {"target": "code", "op": "contains", "value": "Random"}, {"target": "code", "op": "contains", "value": "nextInt"}, {"target": "stdout", "op": "containsLine", "value": "Row 4: O O O O"}, {"target": "stdout", "op": "contains", "value": "SPLASH!"}, {"target": "stdout", "op": "containsLine", "value": "GAME OVER — the rack is empty. Chug up!"}]}$j$::jsonb
),

-- ─────────────────────────── DAY 3 — classes & objects / projects ───────────────────────────
(
  'person-class', 'code', 'Person class',
  $txt$Warm-up: make a Person class with fields name and age, a constructor Person(String n, int a), display() that prints "Niek (25 years old)", and birthday() that adds a year.$txt$,
  $txt$display(): System.out.println(name + " (" + age + " years old)");$txt$,
  NULL,
  jsonb_build_object(
    'starterFiles', jsonb_build_array(
      jsonb_build_object('name', 'Main.java', 'content', $java$public class Main {
    public static void main(String[] args) {
        Person p = new Person("Niek", 25);
        p.display();
        p.birthday();
        p.display();
    }
}
$java$),
      jsonb_build_object('name', 'Person.java', 'content', $java$public class Person {
    // fields: name, age

    // constructor Person(String n, int a)

    // void display()  -> "Niek (25 years old)"

    // void birthday() -> age + 1
}
$java$)),
    'entryClass', 'Main'),
  to_jsonb(jsonb_build_array(
    jsonb_build_object('name', 'Main.java', 'content', $java$public class Main {
    public static void main(String[] args) {
        Person p = new Person("Niek", 25);
        p.display();
        p.birthday();
        p.display();
    }
}
$java$),
    jsonb_build_object('name', 'Person.java', 'content', $java$public class Person {
    String name;
    int age;

    public Person(String n, int a) {
        name = n;
        age = a;
    }

    public void display() {
        System.out.println(name + " (" + age + " years old)");
    }

    public void birthday() {
        age = age + 1;
    }
}
$java$)
  )),
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
    'starterFiles', jsonb_build_array(
      jsonb_build_object('name', 'Main.java', 'content', $java$public class Main {
    public static void main(String[] args) {
        FlightTicket t = new FlightTicket("CPH", "JFK", 7500);
        t.show();
        t.discount();
        t.show();
        for (int i = 0; i < 20; i++) { t.discount(); }
        t.show();
    }
}
$java$),
      jsonb_build_object('name', 'FlightTicket.java', 'content', $java$public class FlightTicket {
    // fields: from, to, price

    // constructor FlightTicket(String f, String t, int p)

    // void show()     -> "CPH --> JFK (7500 DKK)"

    // void discount() -> 500 DKK off, but never below 0
}
$java$)),
    'entryClass', 'Main'),
  to_jsonb(jsonb_build_array(
    jsonb_build_object('name', 'Main.java', 'content', $java$public class Main {
    public static void main(String[] args) {
        FlightTicket t = new FlightTicket("CPH", "JFK", 7500);
        t.show();
        t.discount();
        t.show();
        for (int i = 0; i < 20; i++) { t.discount(); }
        t.show();
    }
}
$java$),
    jsonb_build_object('name', 'FlightTicket.java', 'content', $java$public class FlightTicket {
    String from;
    String to;
    int price;

    public FlightTicket(String f, String t, int p) {
        from = f;
        to = t;
        price = p;
    }

    public void show() {
        System.out.println(from + " --> " + to + " (" + price + " DKK)");
    }

    public void discount() {
        if (price >= 500) {
            price = price - 500;
        }
    }
}
$java$)
  )),
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
    'starterFiles', jsonb_build_array(
      jsonb_build_object('name', 'Main.java', 'content', $java$public class Main {
    public static void main(String[] args) {
        Container c = new Container("AX35", 30);
        c.addCargo(23);
        c.show();
        c.addCargo(40);
        c.show();
    }
}
$java$),
      jsonb_build_object('name', 'Container.java', 'content', $java$public class Container {
    // fields: id, amount, max

    // constructor Container(String i, int max)  -> amount = 0

    // void show()           -> "Container: AX35 (23/30)"

    // void addCargo(int a)  -> add boxes, but never above max
}
$java$)),
    'entryClass', 'Main'),
  to_jsonb(jsonb_build_array(
    jsonb_build_object('name', 'Main.java', 'content', $java$public class Main {
    public static void main(String[] args) {
        Container c = new Container("AX35", 30);
        c.addCargo(23);
        c.show();
        c.addCargo(40);
        c.show();
    }
}
$java$),
    jsonb_build_object('name', 'Container.java', 'content', $java$public class Container {
    String id;
    int amount;
    int max;

    public Container(String i, int maximum) {
        id = i;
        max = maximum;
        amount = 0;
    }

    public void show() {
        System.out.println("Container: " + id + " (" + amount + "/" + max + ")");
    }

    public void addCargo(int a) {
        if (amount + a <= max) {
            amount = amount + a;
        }
    }
}
$java$)
  )),
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
;

-- ─────────────────────────── set memberships ───────────────────────────
--   Day 1: 0–8 (9)   Day 2: 9–32 (24)   Day 3: 33–38 (6)   total 39

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
  ('at-itu-welcome', 9),
  ('scrollbar-friday', 10),
  ('is-friday-boolean', 11),
  ('canteen-lunch', 12),
  ('fitness-access', 13),
  ('student-day-place', 14),
  ('while-five-lines', 15),
  ('while-loop-quiz-1', 16),
  ('while-loop-quiz-2', 17),
  ('while-loop-quiz-3', 18),
  ('while-loop-quiz-4', 19),
  ('while-loop-quiz-5', 20),
  ('while-loop-quiz-6', 21),
  ('analog-tickets-while', 22),
  ('analog-tickets-for', 23),
  ('for-loop-quiz-1', 24),
  ('for-loop-quiz-2', 25),
  ('for-loop-quiz-3', 26),
  ('for-loop-quiz-4', 27),
  ('for-loop-quiz-5', 28),
  ('for-loop-quiz-6', 29),
  ('gym-workout', 30),
  ('analog-reusable-cup-stamps', 31),
  ('beerpong-at-scrollbar', 32),
  ('person-class', 33),
  ('flight-ticket-class', 34),
  ('container-class', 35),
  ('build-a-tree', 36),
  ('grandpas-time-machine', 37),
  ('grandmas-blackmarket-kitchen', 38)
),
resolved AS (
  SELECT t.id AS assignment_id, o.ord
  FROM ordered o
  JOIN assignment t ON t.slug = o.slug
)
INSERT INTO assignment_set_assignment (assignment_set_id, assignment_id, order_index)
SELECT 'day1-2026', assignment_id, ord         FROM resolved WHERE ord BETWEEN 0 AND 8
UNION ALL
SELECT 'day2-2026', assignment_id, ord - 9     FROM resolved WHERE ord BETWEEN 9 AND 32
UNION ALL
SELECT 'day3-2026', assignment_id, ord - 33    FROM resolved WHERE ord BETWEEN 33 AND 38
UNION ALL
SELECT 'all-assignments-for-solo-2026', assignment_id, ord FROM resolved;

DO $check$
DECLARE n int;
BEGIN
  SELECT count(*) INTO n FROM assignment_set_assignment WHERE assignment_set_id = 'all-assignments-for-solo-2026';
  IF n <> 39 THEN
    RAISE EXCEPTION 'seed error: expected 39 assignments in all-assignments-for-solo-2026, got % (typo in a slug?)', n;
  END IF;
END
$check$;

COMMIT;
