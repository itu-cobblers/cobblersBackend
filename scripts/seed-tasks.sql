-- ============================================================================
-- seed-tasks.sql — the 35 BootIT tasks + tasksets, migrated from the
-- frontend's hardcoded bundle (cobblersFrontend/src/lib/tasks.ts).
--
-- Usage (local or VM — schema must already exist via `dotnet ef database update`):
--
--     psql "$CONNECTION_STRING" -f scripts/seed-tasks.sql
--
-- Safe to re-run (idempotent):
--   * task_set / task rows are UPSERTed — content edits here overwrite the DB.
--   * task.sample_solution_json is only set on FIRST insert and never
--     overwritten on re-run, so solutions authored directly in the DB survive.
--   * task_set_task memberships are rebuilt from scratch each run.
--
-- Conventions (see SCHEMA.md):
--   * task.id is DB-assigned — never written here. All references go through
--     task.slug (stable natural key, unique).
--   * kind is lowercase text: 'code' | 'predict' | 'project'.
--   * content_json holds the kind-specific payload (camelCase keys), safe to
--     send to students. Grading rules live in grading_json (never sent).
--   * grading_json is the rule DSL evaluated by the backend's TaskGrader:
--       {"all":[...]} / {"any":[...]} / {"not":...} /
--       {"target":"stdout"|"code","op":"contains"|"containsLine","value":...} /
--       {"target":...,"op":"regex","pattern":...,"flags":"i"?} /
--       {"op":"nonEmptyStdout"}
--     NULL = not auto-gradable (projects, NIM) or graded generically (predict).
-- ============================================================================

BEGIN;

-- ──────────────────────────────── tasksets ─────────────────────────────────

INSERT INTO task_set (task_set_id, display_title) VALUES
  ('day1-2026',               'BootIT Day 1 — 2026'),
  ('day2-2026',               'BootIT Day 2 — 2026'),
  ('day3-2026',               'BootIT Day 3 — 2026'),
  ('all-tasks-for-solo-2026', 'BootIT — All Tasks (Solo) 2026')
ON CONFLICT (task_set_id) DO UPDATE SET display_title = EXCLUDED.display_title;

-- ────────────────────────────────── tasks ──────────────────────────────────

INSERT INTO task (slug, kind, title, description, hint, content_json, sample_solution_json, grading_json) VALUES

-- ─────────────────────────── DAY 1 — basics ───────────────────────────
(
  'name-your-cafe', 'code', 'Name your Café shop',
  $txt$Welcome to your very own hygge café! Every café needs a name. Print your café's name to the console — whatever you print appears on the shop board.$txt$,
  $txt$Use System.out.println("My Cozy Café"); inside main, then press Submit.$txt$,
  jsonb_build_object('starter', $java$public class Main {
    public static void main(String[] args) {
        // Print your café's name — it will appear on the shop board.
    }
}
$java$),
  -- Worked example of a sample solution (code kind = one Java source string).
  -- Only set on first insert; re-runs never overwrite it.
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        System.out.println("Your Answer");
    }
}
$java$::text),
  $j${"op": "nonEmptyStdout"}$j$::jsonb
),
(
  'hello-world', 'code', 'Hello, World!',
  $txt$Make the program print exactly: Hello World!$txt$,
  $txt$System.out.println("Hello World!");$txt$,
  jsonb_build_object('starter', $java$public class Main {
    public static void main(String[] args) {
        // Print Hello World!
    }
}
$java$),
  NULL,
  $j${"any": [
    {"target": "stdout", "op": "containsLine", "value": "Hello World!"},
    {"target": "stdout", "op": "containsLine", "value": "Hello, World!"}
  ]}$j$::jsonb
),
(
  'print-three-values', 'code', 'Print three values',
  $txt$Print these three things, each on its own line: 2024, a greeting like "Hello, my name is …!", and -273.15.$txt$,
  $txt$Three System.out.println(...) statements — an int, a String, and a double.$txt$,
  jsonb_build_object('starter', $java$public class Main {
    public static void main(String[] args) {
        // Print 2024, a greeting, and -273.15
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "contains", "value": "2024"},
    {"target": "stdout", "op": "contains", "value": "-273.15"}
  ]}$j$::jsonb
),
(
  'use-variables', 'code', 'Use variables',
  $txt$Print the same three values, but this time store them in variables first: year (int), name (String), abs_nul (double). Remember: different values need different types.$txt$,
  $txt$int year = 2024; double abs_nul = -273.15; then println each.$txt$,
  jsonb_build_object('starter', $java$public class Main {
    public static void main(String[] args) {
        // Declare year, name, abs_nul — then print them
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "contains", "value": "2024"},
    {"target": "stdout", "op": "contains", "value": "-273.15"}
  ]}$j$::jsonb
),
(
  'your-age', 'code', 'Your age',
  $txt$Using a variable called age (= 26), print "I am 26 years old". Then print "Next year I will be 27!" still using age.$txt$,
  $txt$Print age, then age + 1. Numbers join to text with +.$txt$,
  jsonb_build_object('starter', $java$public class Main {
    public static void main(String[] args) {
        int age = 26;
        // Print the two sentences using age
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "contains", "value": "26 years old"},
    {"target": "stdout", "op": "contains", "value": "27"}
  ]}$j$::jsonb
),
(
  'concatenate-strings', 'code', 'Concatenate strings',
  $txt$Given the words "Hello", "my", "friend", print them as a single line: Hello my friend!$txt$,
  $txt$Join with +: first + " " + second + " " + third + "!"$txt$,
  jsonb_build_object('starter', $java$public class Main {
    public static void main(String[] args) {
        String first = "Hello";
        String second = "my";
        String third = "friend";
        // Print: Hello my friend!
    }
}
$java$),
  NULL,
  $j${"target": "stdout", "op": "containsLine", "value": "Hello my friend!"}$j$::jsonb
),
(
  'currency-converter', 'code', 'Currency converter',
  $txt$Convert 100 DKK to euro (1 euro = 7.45 kr) and print e.g. "100 kr corresponds to 13.42 euro".$txt$,
  $txt$double eur = dkk / 7.45;$txt$,
  jsonb_build_object('starter', $java$public class Main {
    public static void main(String[] args) {
        int dkk = 100;
        // Convert to euro and print the sentence
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "contains", "value": "kr corresponds to"},
    {"target": "stdout", "op": "contains", "value": "euro"}
  ]}$j$::jsonb
),
(
  'celsius-to-fahrenheit', 'code', 'Celsius → Fahrenheit',
  $txt$Fahrenheit is Celsius × 1.8, then + 32. For c = 37.5, print "37.5C is the same as 99.5F".$txt$,
  $txt$double f = (c * 1.8) + 32;$txt$,
  jsonb_build_object('starter', $java$public class Main {
    public static void main(String[] args) {
        double c = 37.5;
        // Compute f and print the sentence
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "stdout", "op": "contains", "value": "37.5"},
    {"target": "stdout", "op": "contains", "value": "99.5"}
  ]}$j$::jsonb
),
(
  'two-functions', 'code', 'Two functions',
  $txt$Refactor the temperature conversion into two parameterised functions: c2f(double c) and f2c(double f), each printing the result.$txt$,
  $txt$static void c2f(double c) { ... }  — call it from main.$txt$,
  jsonb_build_object('starter', $java$public class Temperature {
    static void c2f(double c) {
        // print Celsius as Fahrenheit
    }

    static void f2c(double f) {
        // print Fahrenheit as Celsius
    }

    public static void main(String[] args) {
        c2f(37.5);
        f2c(99.5);
    }
}
$java$),
  NULL,
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "c2f\\s*\\("},
    {"target": "code", "op": "regex", "pattern": "f2c\\s*\\("}
  ]}$j$::jsonb
),
(
  'bmi-calculator', 'code', 'BMI calculator',
  $txt$BMI is weight (kg) divided by height (m) squared. For height 195 cm and weight 84.5 kg, compute and print the BMI.$txt$,
  $txt$Convert cm to m (÷100), then bmi = weight / (heightM * heightM).$txt$,
  jsonb_build_object('starter', $java$public class Main {
    public static void main(String[] args) {
        double weight = 84.5;
        double heightCM = 195;
        // Compute BMI and print it (include the word BMI)
    }
}
$java$),
  NULL,
  $j${"target": "stdout", "op": "regex", "pattern": "bmi", "flags": "i"}$j$::jsonb
),

-- ─────────────── DAY 2 — conditionals, loops, input ───────────────
(
  'is-it-daytime', 'code', 'Is it daytime?',
  $txt$Given an hour (0–23), print "Yes, it is daytime!" from 08:00 onward, otherwise "No, it is nighttime."$txt$,
  $txt$Use if (time >= 8) { ... } else { ... }$txt$,
  jsonb_build_object('starter', $java$public class Main {
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
  jsonb_build_object('starter', $java$public class Number {
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
  jsonb_build_object('starter', $java$public class Numbers {
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
  jsonb_build_object('starter', $java$public class TimeOfDay {
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
  jsonb_build_object('starter', $java$public class Main {
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
    'stdin', E'50\n25\n37\n31\n34\n'),
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
  jsonb_build_object('starter', $java$import java.util.*;

public class Nim {
    public static void main(String[] args) {
        Scanner scanner = new Scanner(System.in);
        int matches = 13;
        // Take turns until the matches run out
    }
}
$java$),
  NULL,
  NULL  -- no auto-grading (same as the frontend: NIM never auto-completes)
),

-- ─────────────── DAY 2 — predict-the-output loop quizzes ───────────────
-- Predict tasks carry no grading_json: they are graded generically from
-- content_json.expectedOutput / accept[] (see SCHEMA.md).
(
  'while-loop-quiz-1', 'predict', 'While Loop Quiz 1',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 10;
while (i > 0) {
    System.out.println(i);
    i = i - 1;
}$java$,
    'expectedOutput', E'10\n9\n8\n7\n6\n5\n4\n3\n2\n1'),
  NULL, NULL
),
(
  'while-loop-quiz-2', 'predict', 'While Loop Quiz 2',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 1;
while (i <= 10) {
    System.out.println(i);
    i = i + 2;
}$java$,
    'expectedOutput', E'1\n3\n5\n7\n9'),
  NULL, NULL
),
(
  'while-loop-quiz-3', 'predict', 'While Loop Quiz 3',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 1;
while (i < 100) {
    System.out.println(i);
    i = i * 2;
}$java$,
    'expectedOutput', E'1\n2\n4\n8\n16\n32\n64'),
  NULL, NULL
),
(
  'while-loop-quiz-4', 'predict', 'While Loop Quiz 4',
  $txt$Careful with this one! Predict what it prints — some loops never stop.$txt$,
  $txt$What is 1 × 1? Does i ever change?$txt$,
  jsonb_build_object(
    'snippet', $java$int i = 1;
while (i < 42) {
    System.out.println(i);
    i = i * i;
}$java$,
    'expectedOutput', 'infinite loop',
    'accept', jsonb_build_array('infinite', 'never stops', 'never ends', 'forever', 'loops forever', 'does not stop', 'doesn''t stop')),
  NULL, NULL
),
(
  'while-loop-quiz-5', 'predict', 'While Loop Quiz 5',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 0;
while (i <= 15) {
    System.out.println(i);
    i = i + 3;
}$java$,
    'expectedOutput', E'0\n3\n6\n9\n12\n15'),
  NULL, NULL
),
(
  'while-loop-quiz-6', 'predict', 'While Loop Quiz 6',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$int i = 64;
while (i >= 2) {
    System.out.println(i);
    i = i / 2;
}$java$,
    'expectedOutput', E'64\n32\n16\n8\n4\n2'),
  NULL, NULL
),
(
  'for-loop-quiz-1', 'predict', 'For Loop Quiz 1',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 10; i > 0; i = i - 2) {
    System.out.println(i);
}$java$,
    'expectedOutput', E'10\n8\n6\n4\n2'),
  NULL, NULL
),
(
  'for-loop-quiz-2', 'predict', 'For Loop Quiz 2',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 1; i < 10; i = i + 3) {
    System.out.println(i);
}$java$,
    'expectedOutput', E'1\n4\n7'),
  NULL, NULL
),
(
  'for-loop-quiz-3', 'predict', 'For Loop Quiz 3',
  $txt$Careful with this one! Predict what it prints — some loops never stop.$txt$,
  $txt$What is 1 × 1? Does i ever grow?$txt$,
  jsonb_build_object(
    'snippet', $java$for (int i = 1; i < 10; i = i * i) {
    System.out.println(i);
}$java$,
    'expectedOutput', 'infinite loop',
    'accept', jsonb_build_array('infinite', 'never stops', 'never ends', 'forever', 'loops forever', 'does not stop', 'doesn''t stop')),
  NULL, NULL
),
(
  'for-loop-quiz-4', 'predict', 'For Loop Quiz 4',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 0; i <= 15; i = i + 3) {
    System.out.println(i);
}$java$,
    'expectedOutput', E'0\n3\n6\n9\n12\n15'),
  NULL, NULL
),
(
  'for-loop-quiz-5', 'predict', 'For Loop Quiz 5',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 1; i <= 10000; i = i * 10) {
    System.out.println(i);
}$java$,
    'expectedOutput', E'1\n10\n100\n1000\n10000'),
  NULL, NULL
),
(
  'for-loop-quiz-6', 'predict', 'For Loop Quiz 6',
  $txt$Read the loop and predict exactly what it prints.$txt$,
  NULL,
  jsonb_build_object(
    'snippet', $java$for (int i = 64; i >= 2; i = i / 2) {
    System.out.println(i);
}$java$,
    'expectedOutput', E'64\n32\n16\n8\n4\n2'),
  NULL, NULL
),

-- ─────────────── DAY 3 — classes & objects (harness-graded) ───────────────
(
  'person-class', 'code', 'Person class',
  $txt$Warm-up: make a Person class with fields name and age, a constructor Person(String n, int a), display() that prints "Niek (25 years old)", and birthday() that adds a year.$txt$,
  $txt$display(): System.out.println(name + " (" + age + " years old)");$txt$,
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

-- ─────────────── DAY 3 — mini-projects (multi-file upload) ───────────────
-- No grading_json: projects are manually reviewed (Submission.Passed stays null).
(
  'build-a-tree', 'project', 'Build a Tree',
  $txt$Mini-project: model a growing (and eventually dying, occasionally blooming) tree.$txt$,
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
  NULL, NULL
),
(
  'grandpas-time-machine', 'project', 'Grandpa''s Time Machine',
  $txt$Mini-project: a text-based time machine that travels between years.$txt$,
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
  NULL, NULL
),
(
  'grandmas-blackmarket-kitchen', 'project', 'Grandma''s Blackmarket Kitchen',
  $txt$Mini-project: a catering planner that assigns menus and dodges the police.$txt$,
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
  NULL, NULL
)

ON CONFLICT (slug) DO UPDATE SET
  kind          = EXCLUDED.kind,
  title         = EXCLUDED.title,
  description   = EXCLUDED.description,
  hint          = EXCLUDED.hint,
  content_json  = EXCLUDED.content_json,
  grading_json  = EXCLUDED.grading_json;
  -- sample_solution_json intentionally NOT updated: solutions authored in the
  -- DB after the first run must survive re-seeding.

-- ─────────────────────────── set memberships ───────────────────────────
-- Rebuilt from scratch every run. One canonical ordered list (the frontend's
-- original 0–34 order); the per-day sets are slices of it, re-numbered from 0.

DELETE FROM task_set_task
WHERE task_set_id IN ('day1-2026', 'day2-2026', 'day3-2026', 'all-tasks-for-solo-2026');

WITH ordered(slug, ord) AS (VALUES
  ('name-your-cafe',                0),
  ('hello-world',                   1),
  ('print-three-values',            2),
  ('use-variables',                 3),
  ('your-age',                      4),
  ('concatenate-strings',           5),
  ('currency-converter',            6),
  ('celsius-to-fahrenheit',         7),
  ('two-functions',                 8),
  ('bmi-calculator',                9),
  ('is-it-daytime',                10),
  ('big-or-small',                 11),
  ('even-or-odd',                  12),
  ('time-of-day',                  13),
  ('multiplication-series',        14),
  ('guess-the-number',             15),
  ('nim-bonus',                    16),
  ('while-loop-quiz-1',            17),
  ('while-loop-quiz-2',            18),
  ('while-loop-quiz-3',            19),
  ('while-loop-quiz-4',            20),
  ('while-loop-quiz-5',            21),
  ('while-loop-quiz-6',            22),
  ('for-loop-quiz-1',              23),
  ('for-loop-quiz-2',              24),
  ('for-loop-quiz-3',              25),
  ('for-loop-quiz-4',              26),
  ('for-loop-quiz-5',              27),
  ('for-loop-quiz-6',              28),
  ('person-class',                 29),
  ('flight-ticket-class',          30),
  ('container-class',              31),
  ('build-a-tree',                 32),
  ('grandpas-time-machine',        33),
  ('grandmas-blackmarket-kitchen', 34)
),
resolved AS (
  SELECT t.id AS task_id, o.ord
  FROM ordered o
  JOIN task t ON t.slug = o.slug
)
INSERT INTO task_set_task (task_set_id, task_id, order_index)
SELECT 'day1-2026', task_id, ord        FROM resolved WHERE ord BETWEEN 0 AND 9
UNION ALL
SELECT 'day2-2026', task_id, ord - 10   FROM resolved WHERE ord BETWEEN 10 AND 28
UNION ALL
SELECT 'day3-2026', task_id, ord - 29   FROM resolved WHERE ord BETWEEN 29 AND 34
UNION ALL
SELECT 'all-tasks-for-solo-2026', task_id, ord FROM resolved;

-- Fail loudly (rolling back the whole transaction) if any slug in the ordered
-- list above didn't resolve to a task row — a silent JOIN drop would otherwise
-- leave a gap in order_index.
DO $check$
DECLARE n int;
BEGIN
  SELECT count(*) INTO n FROM task_set_task WHERE task_set_id = 'all-tasks-for-solo-2026';
  IF n <> 35 THEN
    RAISE EXCEPTION 'seed error: expected 35 tasks in all-tasks-for-solo-2026, got % (typo in a slug?)', n;
  END IF;
END
$check$;

COMMIT;
