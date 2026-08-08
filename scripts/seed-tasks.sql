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
-- Counts: Day 1 = 9, Day 2 = 24, Day 3 = 7, total = 40.
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
  $txt$Now it is your turn: **print a sentence** to say hello to your new university. For example: Hello ITU!$txt$,
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
  $j${"op": "nonEmptyStdout", "message": "Print something to say hello — any message works, just make sure it prints."}$j$::jsonb
),
(
  'print-three-values', 'code', 'Self introduction',
  $txt$Your new friend is interested to know you better. Here are 3 questions from them, in order:
1. What's your name? — print it as **text**
2. Where do you live, in Danish zip code? — print it as a **whole number**
3. How long have you been here, in decimal years? — print it as a **decimal number** (1.0 for exactly one year, 3.5 for three and a half)

**Print your three answers** in that exact order, each on its own line. Example:
IT University of Copenhagen
2300
26.9

Note: if you don't feel like answering with your actual info, that's fine — just print any 1. text, 2. whole number, 3. number with a decimal point, in that order.$txt$,
  $txt$Three println statements, in order: a String, then an int, then a double.$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$println can print more than text — whole numbers and decimal numbers work too. Notice that numbers need no quotes:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$System.out.println("Hello World!");
System.out.println(42);
System.out.println(3.14);$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {
    public static void main(String[] args) {
        // 1) your name (text)  2) Danish zip code (whole number)  3) years here (with a decimal)
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        System.out.println("IT University of Copenhagen");
        System.out.println(2300);
        System.out.println(26.9);
    }
}
$java$::text),
  $j${"all": [
    {"target": "stdout", "op": "regex", "pattern": "(?:^|\\n)[^\\n]*[A-Za-z]{2,}[^\\n]*(?=\\n|$)[\\s\\S]*?(?:^|\\n)-?\\d+(?=\\n|$)[\\s\\S]*?(?:^|\\n)-?\\d+\\.\\d+(?=\\n|$)", "message": "Print three lines in order — text (your name), then a whole number (zip code), then a decimal number (years here) — each on its own line."},
    {"target": "code", "op": "regex", "pattern": "println\\s*\\(\\s*\"[^\"]*[A-Za-z][^\"]*\"\\s*\\)[\\s\\S]*?println\\s*\\(\\s*-?\\d+\\s*\\)[\\s\\S]*?println\\s*\\(\\s*-?\\d+\\.\\d+\\s*\\)", "message": "The zip code and years-here values must be printed as actual numbers, not text in quotes — println(2300), not println(\"2300\")."}
  ]}$j$::jsonb
),
(
  'use-variables', 'code', 'Self introduction (variables)',
  $txt$Another new friend is asking you the same 3 questions:
1. What's your name? — as **text**
2. Where do you live, in Danish zip code? — as a **whole number**
3. How long have you been here, in decimal years? — as a **decimal number** (1.0 for exactly one year, 3.5 for three and a half)

This time, **store each answer in a variable** first, so you can reuse it easily instead of repeating yourself. **Then print the three variables** in that exact order, each on its own line. Example:
IT University of Copenhagen
2300
26.9

Note: if you don't feel like answering with your actual info, that's fine — just store any 1. text, 2. whole number, 3. number with a decimal point, in that order.$txt$,
  $txt$String name = "…"; then System.out.println(name);$txt$,
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
        String name = "IT University of Copenhagen";
        int zipCode = 2300;
        double yearsHere = 26.9;

        System.out.println(name);
        System.out.println(zipCode);
        System.out.println(yearsHere);
    }
}
$java$::text),
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bString\\s+(\\w+)\\s*=\\s*\"[^\"]*\"[\\s\\S]*?\\bprintln\\s*\\(\\s*\\1\\s*\\)", "message": "Declare a String variable holding your name, then print that same variable back with println — don't print a hardcoded string."},
    {"target": "code", "op": "regex", "pattern": "\\bint\\s+(\\w+)\\s*=\\s*-?\\d+\\b[\\s\\S]*?\\bprintln\\s*\\(\\s*\\1\\s*\\)", "message": "Declare an int variable for your birth year, then print that same variable back with println — don't print a hardcoded number."},
    {"target": "code", "op": "regex", "pattern": "\\bdouble\\s+(\\w+)\\s*=\\s*-?\\d+\\.\\d+\\b[\\s\\S]*?\\bprintln\\s*\\(\\s*\\1\\s*\\)", "message": "Declare a double variable (with a decimal point) for years in Copenhagen, then print that same variable back with println — don't print a hardcoded number."}
  ]}$j$::jsonb
),
(
  'variable-assignment', 'code', 'Variable assignment',
  $txt$Use one **int** variable called **age** (starting at 27) to print "ITU has been open for 27", then print the variable value after. Then **update** the **SAME variable**, and print "Next year, ITU will have been open for 28".$txt$,
  $txt$Build each line with two statements: System.out.print for the text, then System.out.println for the number.$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$A variable can be given a new value later — that is why it is called a variable. The same variable then represents a different value:$txt$),
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
        // Print both lines — update age in between
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        int age = 27;
        System.out.print("ITU has been open for ");
        System.out.println(age);

        age = age + 1;
        System.out.print("ITU will have been open for ");
        System.out.println(age);
    }
}
$java$::text),
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bint\\s+age\\s*=\\s*27\\b[\\s\\S]*?\\b(?:print|println)\\s*\\(\\s*age\\s*\\)", "message": "Print the age variable itself (print or println(age)) — don't print a hardcoded number."},
    {"target": "code", "op": "regex", "pattern": "\\bage\\s*(?:=\\s*age\\s*\\+\\s*1|\\+\\+|\\+=\\s*1)\\b", "message": "Reassign the same age variable that started at 27 — age = age + 1, age++, or age += 1 — don't declare a second variable or hardcode 28."},
    {"target": "code", "op": "regex", "pattern": "\\bage\\s*(?:=\\s*age\\s*\\+\\s*1|\\+\\+|\\+=\\s*1)\\b[\\s\\S]*?\\b(?:print|println)\\s*\\(\\s*age\\s*\\)", "message": "After reassigning age, print the variable again (print or println(age)) — don't print a hardcoded number."}
  ]}$j$::jsonb
),
(
  'operators', 'code', 'Operators',
  $txt$Fun fact: According to ITU's 2025 figures, about 23.4% of students are international. The Master in Software Design program admits around 130 students per year.' ||
                            '
Write code to **calculate and print** the estimated number of international students in this program. $txt$,
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
  $j${"all": [
    {"target": "stdout", "op": "regex", "pattern": "^30\\.42\\s*$", "message": "Output should be exactly 30.42 — only the number, nothing else before or after it."},
    {"target": "code", "op": "regex", "pattern": "\\d+\\.\\d+", "message": "Use a decimal (double) literal like 23.4 or 100.0 somewhere in your calculation — don't rely on pure integer math."},
    {"target": "code", "op": "contains", "value": "*", "message": "Use * (multiply) in your calculation."},
    {"target": "code", "op": "contains", "value": "/", "message": "Use / (divide) in your calculation."}
  ]}$j$::jsonb
),
(
  'string-concatenation', 'code', 'String concatenation',
  $txt$Ask the person sitting next to you for their name, then modify the code to greet them personally: Hello my friend, {Name}! **Declare a variable** to hold the name, and print the **concatenated sentence**.$txt$,
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
        String greet = "Hello my friend, ";
        // Declare a variable to hold the name, and print the concatenated sentence.
    }
}
$java$
  ),
  to_jsonb($java$public class Main {
    public static void main(String[] args) {
        String greet = "Hello my friend, ";
        String name = "Aiting";
        System.out.println(greet + name + "!");
    }
}
$java$::text),
  $j${"all": [
    {"target": "code", "op": "contains", "value": "+", "message": "Use + to concatenate the strings."},
    {"target": "code", "op": "regex", "pattern": "\\b(?:print|println)\\s*\\(", "message": "Print the concatenated sentence with print/println."}
  ]}$j$::jsonb
),
(
  'kroner-to-euro', 'code', 'Kroner to euro',
  $txt$Modify the code so it **converts** the opposite way: **from kroner to euro**. 
  For a 20 dkk coffee, print: "20 dkk corresponds to {eur} euro." 

 Note: All the decimals are fine, that is just how doubles print.$txt$,
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
        /*
         * DKK = Danish Crown, abbreviated as kr
         * EUR = Euros
         * The exchange rate is 1 eur = 7.45 dkk
         */
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
  $j${"all": [
    {"target": "stdout", "op": "contains", "value": "20", "message": "Print the dkk amount, 20, in your output line."},
    {"target": "stdout", "op": "contains", "value": "corresponds to", "message": "Use the phrase \"corresponds to\" in your output line."},
    {"target": "stdout", "op": "contains", "value": "euro", "message": "Say \"euro\" (not \"eur\") in your output line."},
    {"target": "stdout", "op": "regex", "pattern": "2\\.68", "message": "Print 20 dkk converted to euro — should come out to approximately 2.68."},
    {"target": "code", "op": "regex", "pattern": "/\\s*7\\.45", "message": "Divide by 7.45 to convert dkk to euro (don't multiply)."}
  ]}$j$::jsonb
),
(
  'functions', 'code', 'Methods',
  $txt$Declare a new **method eurToDkk()** that converts 100 euro to dkk and **prints** "{eur} euro corresponds to {dkk} kr". **Call** it from main, after dkkToEur().$txt$,
  $txt$static void eurToDkk() { double eur = 100; double dkk = eur * 7.45; … }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$A method wraps a snippet of code and gives it a name, so it can be reused just by calling that name:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$public class Main {
    static void dkkToEur() {
        double dkk = 100;
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    public static void main(String[] args) {
        dkkToEur();
    }
}$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$Converting the other way (euro to dkk) is another method, with its own body — multiply instead of divide.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {

    static void dkkToEur() {
        double dkk = 100;
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    // Declare eurToDkk() here

    public static void main(String[] args) {
        dkkToEur();
        // call eurToDkk() here
    }
}
$java$
  ),
  to_jsonb($java$public class Main {

    static void dkkToEur() {
        double dkk = 100;
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    static void eurToDkk() {
        double eur = 100;
        double dkk = eur * 7.45;
        System.out.println(eur + " euro corresponds to " + dkk + " kr");
    }

    public static void main(String[] args) {
        dkkToEur();
        eurToDkk();
    }
}
$java$::text),
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "static\\s+void\\s+eurToDkk\\s*\\(\\s*\\)", "message": "Declare static void eurToDkk() with no parameters."},
    {"target": "code", "op": "regex", "pattern": "eurToDkk\\s*\\(\\s*\\)\\s*;", "message": "Call eurToDkk() from main."},
    {"target": "code", "op": "regex", "pattern": "\\*\\s*7\\.45", "message": "Multiply by 7.45 to convert eur to dkk (don't divide)."},
    {"target": "stdout", "op": "contains", "value": "100.0 euro corresponds to 745.0 kr", "message": "Print \"100.0 euro corresponds to 745.0 kr\" from eurToDkk()."}
  ]}$j$::jsonb
),
(
  'functions-with-parameters', 'code', 'Methods with Parameters',
  $txt$The previous dkkToEur and eurToDkk always convert a fixed 100. **Rewrite** both so they take a **parameter** instead:

- dkkToEur(double dkk) — prints "{dkk} kr corresponds to {eur} euro"
- eurToDkk(double eur) — prints "{eur} euro corresponds to {dkk} kr"

**Call** dkkToEur(100) and eurToDkk(100) from main — the output should match the previous exercise, but now the same methods work for any amount.$txt$,
  $txt$static void dkkToEur(double dkk) { … }   static void eurToDkk(double eur) { … }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$A parameter goes inside the parentheses in the method's declaration, with a type and a name. Whatever value the caller passes in is bound to that name inside the method body:$txt$),
    jsonb_build_object('kind', 'code', 'code', $txt$public class Main {
    static void dkkToEur(double dkk) {
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    public static void main(String[] args) {
        dkkToEur(100);
        dkkToEur(20);
    }
}$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$Now the same method works for any amount, instead of always converting a fixed 100.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {

    // Declare dkkToEur(double dkk) here
    // Declare eurToDkk(double eur) here

    public static void main(String[] args) {
        // call dkkToEur(100) here
        // call eurToDkk(100) here
    }

}
$java$
  ),
  to_jsonb($java$public class Main {

    static void dkkToEur(double dkk) {
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    static void eurToDkk(double eur) {
        double dkk = eur * 7.45;
        System.out.println(eur + " euro corresponds to " + dkk + " kr");
    }

    public static void main(String[] args) {
        dkkToEur(100);
        eurToDkk(100);
    }

}
$java$::text),
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "static\\s+void\\s+dkkToEur\\s*\\(\\s*double\\s+\\w+\\s*\\)", "message": "Declare dkkToEur to take one double parameter (the amount in dkk)."},
    {"target": "code", "op": "regex", "pattern": "static\\s+void\\s+eurToDkk\\s*\\(\\s*double\\s+\\w+\\s*\\)", "message": "Declare eurToDkk to take one double parameter (the amount in eur)."},
    {"target": "code", "op": "regex", "pattern": "dkkToEur\\s*\\(\\s*100\\s*\\)\\s*;", "message": "Call dkkToEur(100) from main."},
    {"target": "code", "op": "regex", "pattern": "eurToDkk\\s*\\(\\s*100\\s*\\)\\s*;", "message": "Call eurToDkk(100) from main."},
    {"target": "stdout", "op": "contains", "value": "100.0 kr corresponds to", "message": "dkkToEur(100) should print \"100.0 kr corresponds to ... euro\"."},
    {"target": "stdout", "op": "contains", "value": "100.0 euro corresponds to 745.0 kr", "message": "eurToDkk(100) should print \"100.0 euro corresponds to 745.0 kr\"."}
  ]}$j$::jsonb
),
(
  'your-semester-in-ects', 'code', 'Your semester in ECTS',
  $txt$At ITU every course is worth ECTS points, and a full semester adds up to 30 ECTS. You are starting Software Design. **Write printCourse(String name, double ects, int semester)**, to print one course's line: "{name} ({ects} ECTS) is in {semester} semester".

Then **build one layer on top of it**: a method per semester that calls printCourse for each of that semester's courses.
- printFirstSemester() calls printCourse(): Introductory Programming (15 ECTS), Discrete Mathematics (7.5 ECTS), Software Engineering (7.5 ECTS)
- printSecondSemester() calls printCourse(): Introduction to Database System (7.5 ECTS), Algorithm and Data Structures (7.5 ECTS)

**main** calls **printFirstSemester()** and **printSecondSemester()**.$txt$, null,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$A method with several parameters can print many different values the same way.$txt$),
    jsonb_build_object('kind', 'text', 'text', $txt$A method isn't only called from main — it can call another method too.$txt$),
    jsonb_build_object('kind', 'code', 'code', $java$public class Main {
    static void dkkToEur(double dkk) {
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    static void buyFilterAtAnalog() {
        System.out.println("A cup of filter coffee cost " + 15 + " dkk");
        dkkToEur(15);
    }

    static void buyCapuccianoAtAnalog() {
        System.out.println("A cup of capucciano cost " + 20 + " dkk");
        dkkToEur(20);
    }

    public static void main(String[] args) {
        buyFilterAtAnalog();
        buyCapuccianoAtAnalog();
    }
}$java$),
    jsonb_build_object('kind', 'text', 'text', $txt$That builds layers: a low-level method that does one small thing, and a higher-level method that reuses it several times. The caller of a layer only needs to know what it does, not how.$txt$)
  ),
  jsonb_build_object(
    'starter', $java$public class Main {

    // Declare printCourse(String name, double ects, int semester) here — same as before

    // Declare printFirstSemester() here
    // It takes no parameters — call printCourse for each semester-1 course

    // Declare printSecondSemester() here
    // It takes no parameters — call printCourse for each semester-2 course

    public static void main(String[] args) {
        // Call printFirstSemester() and printSecondSemester() here
        // Don't call printCourse directly from main
    }
}
$java$
  ),
  to_jsonb($java$public class Main {

    static void printCourse(String name, double ects, int semester) {
        System.out.println(name + " (" + ects + " ECTS) is in " + semester + " semester");
    }

    static void printFirstSemester() {
        printCourse("Introductory Programming", 15, 1);
        printCourse("Discrete Mathematics", 7.5, 1);
        printCourse("Software Engineering", 7.5, 1);
    }

    static void printSecondSemester() {
        printCourse("Introduction to Database System", 7.5, 2);
        printCourse("Algorithm and Data Structures", 7.5, 2);
    }

    public static void main(String[] args) {
        printFirstSemester();
        printSecondSemester();
    }
}
$java$::text),
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "static\\s+void\\s+printCourse\\s*\\(\\s*String\\s+\\w+\\s*,\\s*double\\s+\\w+\\s*,\\s*int\\s+\\w+\\s*\\)", "message": "printCourse needs three parameters in order (String, double, int) — ects must be a double so 7.5 doesn't get truncated to 7."},
    {"target": "code", "op": "regex", "pattern": "static\\s+void\\s+printFirstSemester\\s*\\(\\s*\\)", "message": "Declare static void printFirstSemester() with no parameters."},
    {"target": "code", "op": "regex", "pattern": "static\\s+void\\s+printSecondSemester\\s*\\(\\s*\\)", "message": "Declare static void printSecondSemester() with no parameters."},
    {"target": "code", "op": "regex", "pattern": "\\bprintFirstSemester\\s*\\(\\s*\\)\\s*;", "message": "Call printFirstSemester() from main."},
    {"target": "code", "op": "regex", "pattern": "\\bprintSecondSemester\\s*\\(\\s*\\)\\s*;", "message": "Call printSecondSemester() from main."},
    {"target": "code", "op": "regex", "pattern": "public\\s+static\\s+void\\s+main\\s*\\([^)]*\\)\\s*\\{(?:(?!printCourse\\()[\\s\\S])*\\}", "message": "main should only call printFirstSemester() and printSecondSemester() — let those methods call printCourse, not main directly."},
    {"target": "code", "op": "contains", "value": "printCourse(\"Introductory Programming\"", "message": "printFirstSemester should call printCourse for Introductory Programming."},
    {"target": "code", "op": "contains", "value": "printCourse(\"Discrete Mathematics\"", "message": "printFirstSemester should call printCourse for Discrete Mathematics."},
    {"target": "code", "op": "contains", "value": "printCourse(\"Software Engineering\"", "message": "printFirstSemester should call printCourse for Software Engineering."},
    {"target": "code", "op": "contains", "value": "printCourse(\"Introduction to Database System\"", "message": "printSecondSemester should call printCourse for Introduction to Database System."},
    {"target": "code", "op": "contains", "value": "printCourse(\"Algorithm and Data Structures\"", "message": "printSecondSemester should call printCourse for Algorithm and Data Structures."},
    {"target": "stdout", "op": "contains", "value": "Introductory Programming", "message": "Your output should include a line for Introductory Programming."},
    {"target": "stdout", "op": "contains", "value": "Discrete Mathematics", "message": "Your output should include a line for Discrete Mathematics."},
    {"target": "stdout", "op": "contains", "value": "Software Engineering", "message": "Your output should include a line for Software Engineering."},
    {"target": "stdout", "op": "contains", "value": "Introduction to Database System", "message": "Your output should include a line for Introduction to Database System."},
    {"target": "stdout", "op": "contains", "value": "Algorithm and Data Structures", "message": "Your output should include a line for Algorithm and Data Structures."}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bif\\s*\\(\\s*atItu\\s*\\)", "message": "Guard the print with if (atItu) — the print must be conditional on atItu, not unconditional."},
    {"target": "stdout", "op": "containsLine", "value": "Welcome to ITU!", "message": "Print exactly \"Welcome to ITU!\"."}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bif\\s*\\(\\s*weekday\\s*==\\s*\"Friday\"\\s*\\)", "message": "Branch on if (weekday == \"Friday\") { ... } else { ... }."},
    {"any": [
      {"target": "stdout", "op": "containsLine", "value": "Yes, it is Friday, Scrollbar will open today!"},
      {"target": "stdout", "op": "containsLine", "value": "No, Scrollbar is closed."}
    ], "message": "Print the exact sentence for whichever branch runs — \"Yes, it is Friday, Scrollbar will open today!\" or \"No, Scrollbar is closed.\""}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bboolean\\s+isFriday\\s*=\\s*\\(?\\s*weekday\\s*==\\s*\"Friday\"\\s*\\)?", "message": "Declare boolean isFriday = (weekday == \"Friday\");"},
    {"target": "code", "op": "regex", "pattern": "\\bif\\s*\\(\\s*isFriday\\s*\\)", "message": "Branch with if (isFriday) { ... } else { ... } — not if (isFriday == true)."},
    {"any": [
      {"target": "stdout", "op": "containsLine", "value": "Yes, it is Friday, Scrollbar will open today!"},
      {"target": "stdout", "op": "containsLine", "value": "No, Scrollbar is closed."}
    ], "message": "Print the exact sentence for whichever branch runs — \"Yes, it is Friday, Scrollbar will open today!\" or \"No, Scrollbar is closed.\""}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\btime\\s*<\\s*11\\.0\\b", "message": "Check time < 11.0 for the too-early case."},
    {"target": "code", "op": "regex", "pattern": "\\btime\\s*>=\\s*14\\.0\\b", "message": "Check time >= 14.0 for the too-late case."},
    {"target": "code", "op": "regex", "pattern": "\\btime\\s*>=\\s*13\\.75\\b", "message": "Check time >= 13.75 for the late-lunch discount."},
    {"target": "stdout", "op": "containsLine", "value": "Lunch is being served.", "message": "For time = 13.75, print \"Lunch is being served.\" as its own line."},
    {"target": "stdout", "op": "containsLine", "value": "Late lunch discount applies!", "message": "For time = 13.75, print \"Late lunch discount applies!\" as its own line."}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bif\\s*\\(\\s*(?:hasMembership\\s*\\|\\|\\s*isFreeTrialTuesday|isFreeTrialTuesday\\s*\\|\\|\\s*hasMembership)\\s*\\)", "message": "Branch with if (hasMembership || isFreeTrialTuesday) { ... } else { ... }."},
    {"target": "stdout", "op": "containsLine", "value": "Accessed", "message": "With hasMembership=false and isFreeTrialTuesday=true, output should be exactly \"Accessed\" — check your || condition."}
  ]}$j$::jsonb
),
(
  'student-day-place', 'code', 'A common day as an ITU student',
  $txt$Write a method place(String period) that returns where a student probably is:

- morning → lectures, exercises, or a group room
- break → the Cafe Analog coffee queue
- noon → the canteen rush
- afternoon → the outdoor grass (if the sun is out)

From main, set a period and print:
System.out.println("During " + period + ", an ITU student is probably at " + place(period));$txt$,
  $txt$static String place(String period) { return ...; }$txt$,
  jsonb_build_array(
    jsonb_build_object('kind', 'text', 'text', $txt$Previous methods were mostly void (they printed). A method can also return a value to the caller with return. The return type goes where void used to be (String, int, boolean, …).$txt$)
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "static\\s+String\\s+place\\s*\\(\\s*String\\s+\\w+\\s*\\)", "message": "Declare static String place(String period) — a method that returns a String."},
    {"target": "code", "op": "contains", "value": "return", "message": "place(...) must return a value, not print it directly."},
    {"target": "stdout", "op": "containsLine", "value": "During morning, an ITU student is probably at lectures, exercises, or a group room", "message": "For period = \"morning\", place(period) should describe lectures/exercises/group room — check your morning branch."}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bwhile\\s*\\(\\s*i\\s*(?:<\\s*5|<=\\s*4)\\s*\\)", "message": "Change the loop condition on i to stop after exactly 5 lines — while (i < 5) (or i <= 4)."},
    {"target": "stdout", "op": "containsLine", "value": "Line 0: I will push my code before the deadline!", "message": "The first line should read \"Line 0: I will push my code before the deadline!\""},
    {"target": "stdout", "op": "containsLine", "value": "Line 4: I will push my code before the deadline!", "message": "The last (5th) line should read \"Line 4: I will push my code before the deadline!\""},
    {"not": {"target": "stdout", "op": "contains", "value": "Line 5:"}, "message": "There should be no \"Line 5:\" — that would be a 6th line."}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bwhile\\s*\\(", "message": "Use a while loop."},
    {"target": "stdout", "op": "regex", "pattern": "(?:^|\\n)5(?=\\n|$)[\\s\\S]*?(?:^|\\n)10(?=\\n|$)[\\s\\S]*?(?:^|\\n)15(?=\\n|$)[\\s\\S]*?(?:^|\\n)20(?=\\n|$)[\\s\\S]*?(?:^|\\n)25(?=\\n|$)[\\s\\S]*?(?:^|\\n)30(?=\\n|$)[\\s\\S]*?(?:^|\\n)35(?=\\n|$)[\\s\\S]*?(?:^|\\n)40(?=\\n|$)[\\s\\S]*?(?:^|\\n)45(?=\\n|$)[\\s\\S]*?(?:^|\\n)50(?=\\n|$)", "message": "Print the running total after every pack, in order: 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 — one per line."}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bfor\\s*\\(", "message": "Use a for loop."},
    {"not": {"target": "code", "op": "regex", "pattern": "\\bwhile\\s*\\("}, "message": "Rewrite this with a for loop — don't keep a while loop around."},
    {"target": "stdout", "op": "regex", "pattern": "(?:^|\\n)5(?=\\n|$)[\\s\\S]*?(?:^|\\n)10(?=\\n|$)[\\s\\S]*?(?:^|\\n)15(?=\\n|$)[\\s\\S]*?(?:^|\\n)20(?=\\n|$)[\\s\\S]*?(?:^|\\n)25(?=\\n|$)[\\s\\S]*?(?:^|\\n)30(?=\\n|$)[\\s\\S]*?(?:^|\\n)35(?=\\n|$)[\\s\\S]*?(?:^|\\n)40(?=\\n|$)[\\s\\S]*?(?:^|\\n)45(?=\\n|$)[\\s\\S]*?(?:^|\\n)50(?=\\n|$)", "message": "Print the running total after every pack, in order: 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 — one per line."}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "for\\s*\\([^)]*\\)\\s*\\{[^{}]*for\\s*\\([^)]*\\)", "message": "Nest a for loop inside another for loop — one outer for per set, one inner for per rep."},
    {"target": "stdout", "op": "containsLine", "value": "Set 1 Rep 1", "message": "Missing \"Set 1 Rep 1\"."},
    {"target": "stdout", "op": "containsLine", "value": "Set 1 Rep 12", "message": "Missing \"Set 1 Rep 12\" — set 1 should log all 12 reps."},
    {"target": "stdout", "op": "containsLine", "value": "Set 2 Rep 1", "message": "Missing \"Set 2 Rep 1\" — the outer loop should move on to set 2."},
    {"target": "stdout", "op": "containsLine", "value": "Set 3 Rep 1", "message": "Missing \"Set 3 Rep 1\"."},
    {"target": "stdout", "op": "containsLine", "value": "Set 4 Rep 1", "message": "Missing \"Set 4 Rep 1\"."},
    {"target": "stdout", "op": "containsLine", "value": "Set 4 Rep 12", "message": "Missing \"Set 4 Rep 12\" — the last rep of the last set."}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bif\\s*\\(", "message": "Use if/else to tell a normal stamp apart from the 10th (free-cup) stamp."},
    {"target": "code", "op": "regex", "pattern": "\\bfor\\s*\\(|\\bwhile\\s*\\(", "message": "Use a for or while loop over the 24 drinks."},
    {"target": "stdout", "op": "containsLine", "value": "Brought my own cup, got a stamp! (4/10)", "message": "Missing \"Brought my own cup, got a stamp! (4/10)\" — check your stamp message and counter."},
    {"target": "stdout", "op": "regex", "pattern": "(?:Free cup! Here's a new stamp card\\.[\\s\\S]*){2}", "message": "The stamp card should reset and fill up twice in 24 drinks (10 + 10 + 4) — \"Free cup! Here's a new stamp card.\" should appear twice."},
    {"not": {"target": "stdout", "op": "regex", "pattern": "(?:Free cup! Here's a new stamp card\\.[\\s\\S]*){3}"}, "message": "24 drinks should only fill the stamp card twice — a third \"Free cup!\" means your reset logic is off."}
  ]}$j$::jsonb
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
  $j${"all": [
    {"target": "code", "op": "regex", "pattern": "\\bfor\\s*\\(", "message": "Use a for loop to print the triangle rack."},
    {"target": "code", "op": "regex", "pattern": "\\bwhile\\s*\\(", "message": "Use a while loop to simulate the throws."},
    {"target": "code", "op": "contains", "value": "Random", "message": "Create a Random rng = new Random(); to roll for each throw."},
    {"target": "code", "op": "contains", "value": "nextInt", "message": "Use rng.nextInt(4) to roll 0-3 for each throw."},
    {"target": "stdout", "op": "containsLine", "value": "Row 4: O O O O", "message": "Print the full 4-row triangle rack, ending with \"Row 4: O O O O\"."},
    {"target": "stdout", "op": "contains", "value": "SPLASH!", "message": "Print \"Throw {n}: SPLASH! {cupsLeft} cups left.\" on a hit."},
    {"target": "stdout", "op": "containsLine", "value": "GAME OVER — the rack is empty. Chug up!", "message": "Print \"GAME OVER — the rack is empty. Chug up!\" once cupsLeft reaches 0."}
  ]}$j$::jsonb
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
    {"target": "code", "op": "regex", "pattern": "\\bclass\\s+Person\\b", "message": "Define a Person class (in Person.java) — don't just hardcode the output directly in Main."},
    {"target": "code", "op": "regex", "pattern": "\\bage\\s*(?:=\\s*age\\s*\\+\\s*1|\\+\\+|\\+=\\s*1)\\b", "message": "birthday() should increment the age field — age = age + 1, age++, or age += 1."},
    {"target": "stdout", "op": "containsLine", "value": "Niek (25 years old)", "message": "The first display() call should print \"Niek (25 years old)\"."},
    {"target": "stdout", "op": "containsLine", "value": "Niek (26 years old)", "message": "After birthday(), the second display() call should print \"Niek (26 years old)\"."}
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
    {"target": "code", "op": "regex", "pattern": "\\bclass\\s+FlightTicket\\b", "message": "Define a FlightTicket class (in FlightTicket.java) — don't just hardcode the output directly in Main."},
    {"target": "stdout", "op": "containsLine", "value": "CPH --> JFK (7500 DKK)", "message": "The first show() call should print \"CPH --> JFK (7500 DKK)\"."},
    {"target": "stdout", "op": "containsLine", "value": "CPH --> JFK (7000 DKK)", "message": "After one discount(), show() should print \"CPH --> JFK (7000 DKK)\"."},
    {"target": "stdout", "op": "containsLine", "value": "CPH --> JFK (0 DKK)", "message": "After 21 discount() calls the price should floor at exactly 0 — \"CPH --> JFK (0 DKK)\" — not stop early or wrap negative."},
    {"not": {"target": "stdout", "op": "regex", "pattern": "-\\d+\\s*DKK"}, "message": "Price must never go negative — guard discount() so it doesn't subtract past 0."}
  ]}$j$::jsonb
),
(
  'container-class', 'code', 'Container class',
  $txt$Make a Container class: fields id, amount, max; constructor Container(String i, int max) (amount starts at 0); show() prints "Container: AX35 (23/30)"; addCargo(int a) adds boxes. Make sure the container can't be over-filled.

Be strict about what "can't be over-filled" means: if an addCargo(a) would push amount past max, reject that addition in full — amount stays exactly what it was before the call. Don't partially fill up to max instead (e.g. addCargo(40) on a 23/30 container must leave it at 23/30, not jump to 30/30).$txt$,
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
    {"target": "code", "op": "regex", "pattern": "\\bclass\\s+Container\\b", "message": "Define a Container class (in Container.java) — don't just hardcode the output directly in Main."},
    {"target": "stdout", "op": "regex", "pattern": "(?:Container: AX35 \\(23/30\\)[\\s\\S]*){2}", "message": "show() should print \"Container: AX35 (23/30)\" both before and after the rejected addCargo(40) — a full addition over max must be rejected entirely, leaving amount unchanged at 23."},
    {"not": {"target": "stdout", "op": "regex", "pattern": "\\((?:3[1-9]|[4-9]\\d|\\d{3,})/30\\)"}, "message": "amount must never exceed max (30) — addCargo(40) should be rejected in full, not partially applied."}
  ]}$j$::jsonb
),

-- ─────────────────────────── DAY 3 — mini-projects (multi-file upload) ───────────────────────────
-- No grading_json: projects are manually reviewed (Submission.Passed stays null).
-- hint (like every other kind) holds the PDF's "hint" section — never folded
-- into content_json.brief. sample_solution_json is [{name, content}], same
-- shape as a submission — reference files sourced from cobblersSolutions'
-- day3 folders, package declarations stripped (students never declare one).
(
  'build-a-tree', 'project', 'Build a Tree',
  $txt$Pick a project to start with! Choose any of the 4 options and complete as many as youd like. Please write and test your code in VS Code, then drag and drop your files here to submit.$txt$,
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
> The tree is already dead.                   // grow() again$txt$,
    'requiredClasses', jsonb_build_array('Tree'),
    'entryClass', 'Main'),
  jsonb_build_array(
    jsonb_build_object('name', 'Main.java', 'content', $sol$public class Main {
    public static void main(String[] args) {
        Tree tree = new Tree(1.0, 2.0, 10);
        tree.display();

        for (int year = 1; year <= 11; year = year + 1) {
            tree.grow();
            if (year == 5 || year == 11) {
                tree.display();
            }
        }

        tree.grow();
    }
}
$sol$),
    jsonb_build_object('name', 'Tree.java', 'content', $sol$public class Tree {
    private double height;
    private int age;
    private final double growthRate;
    private final int maxAge;
    private boolean alive = true;

    public Tree(double initialHeight, double growthRate, int maxAge) {
        if (initialHeight <= 0 || growthRate <= 0 || maxAge < 0) {
            throw new IllegalArgumentException("Tree values must be positive.");
        }
        this.height = initialHeight;
        this.growthRate = growthRate;
        this.maxAge = maxAge;
    }

    public void display() {
        if (!alive) {
            System.out.println("The tree is dead, and reached the age " + age
                    + " with a height of " + height + "cm.");
            return;
        }

        System.out.println("Your tree is currently " + age + " years old.");
        System.out.println("It has reached the height of " + height + "cm.");
        if (isBlooming()) {
            System.out.println("It is currently blooming.");
        }
    }

    public void grow() {
        if (!alive) {
            System.out.println("The tree is already dead.");
            return;
        }

        age = age + 1;
        height = height * growthRate;
        if (age > maxAge) {
            alive = false;
            System.out.println("The tree has died.");
        } else {
            System.out.println("And your tree just grew a year older!");
        }
    }

    private boolean isBlooming() {
        return age > 0 && age % 5 == 0 && (age % 20 != 0 || age % 100 == 0);
    }
}
$sol$)
  ),
  NULL
),
(
  'grandpas-time-machine', 'project', 'Grandpa''s Time Machine',
  $txt$Pick a project to start with! Choose any of the 4 options and complete as many as youd like. Please write and test your code in VS Code, then drag and drop your files here to submit.$txt$,
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
Tim3M4chin3: You arrived to your destination: 2020$txt$,
    'requiredClasses', jsonb_build_array('TimeMachine'),
    'entryClass', 'Main'),
  jsonb_build_array(
    jsonb_build_object('name', 'Main.java', 'content', $sol$public class Main {
    public static void main(String[] args) {
        TimeMachine machine = new TimeMachine(2016);
        machine.travelTo(2020);

        System.out.println();
        machine.travelTo(2016);
    }
}
$sol$),
    jsonb_build_object('name', 'TimeMachine.java', 'content', $sol$import java.util.Map;

public class TimeMachine {
    private static final Map<Integer, String> EVENTS = Map.of(
            1969, "Humans landed on the Moon.",
            1989, "The Berlin Wall fell.",
            2018, "A lot of awesome people went to BootIT."
    );

    private int currentYear;
    private boolean announceYears = true;
    private boolean announceLeapYears = true;

    public TimeMachine(int currentYear) {
        this.currentYear = currentYear;
    }

    public void travelTo(int destinationYear) {
        announceCurrentYear();
        if (destinationYear == currentYear) {
            System.out.println("Tim3M4chin3: You are already in " + currentYear + ".");
            return;
        }

        int direction = destinationYear > currentYear ? 1 : -1;
        while (currentYear != destinationYear) {
            currentYear = currentYear + direction;
            announceCurrentYear();
        }
        System.out.println("Tim3M4chin3: You arrived at your destination: " + currentYear);
    }

    private void announceCurrentYear() {
        if (announceYears) {
            System.out.println("Tim3M4chin3: Current year is now " + currentYear);
        }
        if (announceLeapYears && isLeapYear(currentYear)) {
            System.out.println("Tim3M4chin3: A leap year just happened WoOoOOoOW!");
        }
        if (EVENTS.containsKey(currentYear)) {
            System.out.println("Tim3M4chin3: " + EVENTS.get(currentYear));
        }
    }

    public static boolean isLeapYear(int year) {
        return year % 400 == 0 || (year % 4 == 0 && year % 100 != 0);
    }
}
$sol$)
  ),
  NULL
),
(
  'grandmas-blackmarket-kitchen', 'project', 'Grandma''s Blackmarket Kitchen',
  $txt$Pick a project to start with! Choose any of the 4 options and complete as many as youd like. Please write and test your code in VS Code, then drag and drop your files here to submit.$txt$,
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
3x 1. red cabbage salad, 2. curry chicken, 3. rødgrød m. fløde$txt$,
    'requiredClasses', jsonb_build_array('Kitchen'),
    'entryClass', 'Main'),
  jsonb_build_array(
    jsonb_build_object('name', 'Main.java', 'content', $sol$import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner scanner = new Scanner(System.in);
        System.out.print("How many people is the order for? ");
        if (!scanner.hasNextInt()) {
            System.out.println("Please enter a whole number.");
            return;
        }
        int people = scanner.nextInt();

        System.out.print("How many are picky eaters? ");
        if (!scanner.hasNextInt()) {
            System.out.println("Please enter a whole number.");
            return;
        }
        int pickyEaters = scanner.nextInt();

        Kitchen kitchen = new Kitchen("Grandma", 42);
        kitchen.planOrder(people, pickyEaters);
    }
}
$sol$),
    jsonb_build_object('name', 'Kitchen.java', 'content', $sol$import java.util.Random;

public class Kitchen {
    private final String owner;
    private final Random random;
    private final Menu firstMenu = new Menu(
            "1. Tarteletter, 2. Stegt flæsk m. persillesovs, 3. citronfromage", 110);
    private final Menu secondMenu = new Menu(
            "1. red cabbage salad, 2. curry chicken, 3. rødgrød m. fløde", 131);

    public Kitchen(String owner, long randomSeed) {
        this.owner = owner;
        this.random = new Random(randomSeed);
    }

    public void planOrder(int people, int pickyEaters) {
        if (people == 8 && pickyEaters == 7) {
            System.out.println(owner + ": Nice try, police. I will not cook this order!");
            return;
        }
        if (people <= 0 || pickyEaters < 0 || pickyEaters > people) {
            System.out.println(owner + ": That order is impossible. Check the number of people.");
            return;
        }

        int firstMenuCount = pickyEaters;
        int secondMenuCount = 0;
        for (int person = pickyEaters; person < people; person = person + 1) {
            if (random.nextBoolean()) {
                firstMenuCount = firstMenuCount + 1;
            } else {
                secondMenuCount = secondMenuCount + 1;
            }
        }

        int totalPrice = firstMenuCount * firstMenu.getPrice()
                + secondMenuCount * secondMenu.getPrice();
        System.out.println(owner + ": I want to cook the following " + people + " menus for you:");
        printMenu(firstMenuCount, firstMenu);
        printMenu(secondMenuCount, secondMenu);
        System.out.println("Total price: " + totalPrice + " DKK");
    }

    private void printMenu(int count, Menu menu) {
        if (count > 0) {
            System.out.println(count + "x " + menu.getDescription());
        }
    }
}
$sol$),
    jsonb_build_object('name', 'Menu.java', 'content', $sol$public class Menu {
    private final String description;
    private final int price;

    public Menu(String description, int price) {
        this.description = description;
        this.price = price;
    }

    public String getDescription() {
        return description;
    }

    public int getPrice() {
        return price;
    }
}
$sol$)
  ),
  NULL
),
(
  'seat-selector', 'project', 'Seat Selector',
  $txt$Pick a project to start with! Choose any of the 4 options and complete as many as youd like. Please write and test your code in VS Code, then drag and drop your files here to submit.$txt$,
  NULL,
  NULL,
  jsonb_build_object(
    'brief', $txt$Seat Selector

The BootIT teachers want every third row of the auditorium kept free — but it's hard to remember which row is the third. Build a program that helps them enforce it.

1) Ask how many rows are in the room — the room size changes, so the program must handle any number of rows.
2) For each row a student names, say whether they may sit there.
3) Handle unreasonable input: a row past the room size, zero, or a negative number.
4) Stop on "STOP", then optionally report how many free rows there are and how many students found a valid seat.

Sketch:
> How many rows are there in the room?
> 10
> Coolio! I've computed which rows that must be free now!
> Which row would the student like to sit in?
> 3
> That's an invalid row! That row must be free. Try another one...
> 4
> That's a brilliant choice. Please take a seat.
> STOP
> Cool, everyone must be seated then! Please enjoy the lecture.$txt$,
    'requiredClasses', jsonb_build_array('SeatSelector'),
    'entryClass', 'Main'),
  jsonb_build_array(
    jsonb_build_object('name', 'Main.java', 'content', $sol$import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner scanner = new Scanner(System.in);
        System.out.println("How many rows are there in the room?");
        if (!scanner.hasNextInt()) {
            System.out.println("Please enter a whole number.");
            return;
        }

        int rows = scanner.nextInt();
        scanner.nextLine();
        if (rows <= 0) {
            System.out.println("The room must have at least one row.");
            return;
        }

        SeatSelector selector = new SeatSelector(rows);
        selector.run(scanner);
    }
}
$sol$),
    jsonb_build_object('name', 'SeatSelector.java', 'content', $sol$import java.util.Scanner;

public class SeatSelector {
    private final int totalRows;
    private int seatedStudents;

    public SeatSelector(int totalRows) {
        if (totalRows <= 0) {
            throw new IllegalArgumentException("A room must have at least one row.");
        }
        this.totalRows = totalRows;
    }

    public void run(Scanner scanner) {
        System.out.println("Coolio! I've computed which rows must be free now!");
        while (true) {
            System.out.println("Which row would the student like to sit in? (or STOP)");
            String input = scanner.nextLine().trim();
            if (input.equalsIgnoreCase("STOP")) {
                finishLecture(scanner);
                return;
            }

            try {
                checkRow(Integer.parseInt(input));
            } catch (NumberFormatException error) {
                System.out.println("Please enter a row number or STOP.");
            }
        }
    }

    private void checkRow(int row) {
        if (row < 0) {
            System.out.println("Negative numbers?! Please pick another seat.");
        } else if (row == 0) {
            System.out.println("Really? The floor? Please select another row.");
        } else if (row > totalRows) {
            System.out.println("Sorry, there are only " + totalRows + " rows. Please try another.");
        } else if (mustBeFree(row)) {
            System.out.println("That's an invalid row! That row must be free. Try another one.");
        } else {
            seatedStudents = seatedStudents + 1;
            System.out.println("That's a brilliant choice. Please take a seat.");
        }
    }

    private boolean mustBeFree(int row) {
        return row % 3 == 0;
    }

    private void finishLecture(Scanner scanner) {
        System.out.println("Cool, everyone must be seated then! Please enjoy the lecture.");
        System.out.println("Do you want to know how many free rows there are? (y/n)");
        String answer = scanner.nextLine().trim();
        if (answer.equalsIgnoreCase("y")) {
            System.out.println("There should be a total of " + totalRows / 3 + " free rows.");
            String noun = seatedStudents == 1 ? " student" : " students";
            System.out.println(seatedStudents + noun + " selected valid rows.");
        } else {
            System.out.println("Okay... Then count it yourself...");
        }
    }
}
$sol$)
  ),
  NULL
)
;

-- ─────────────────────────── set memberships ───────────────────────────
--   Day 1: 0–9 (10)   Day 2: 10–33 (24)   Day 3: 34–40 (7)   total 41

WITH ordered(slug, ord) AS (VALUES
  ('hello-itu', 0),
  ('print-three-values', 1),
  ('use-variables', 2),
  ('variable-assignment', 3),
  ('operators', 4),
  ('string-concatenation', 5),
  ('kroner-to-euro', 6),
  ('functions', 7),
  ('functions-with-parameters', 8),
  ('your-semester-in-ects', 9),
  ('at-itu-welcome', 10),
  ('scrollbar-friday', 11),
  ('is-friday-boolean', 12),
  ('canteen-lunch', 13),
  ('fitness-access', 14),
  ('student-day-place', 15),
  ('while-five-lines', 16),
  ('while-loop-quiz-1', 17),
  ('while-loop-quiz-2', 18),
  ('while-loop-quiz-3', 19),
  ('while-loop-quiz-4', 20),
  ('while-loop-quiz-5', 21),
  ('while-loop-quiz-6', 22),
  ('analog-tickets-while', 23),
  ('analog-tickets-for', 24),
  ('for-loop-quiz-1', 25),
  ('for-loop-quiz-2', 26),
  ('for-loop-quiz-3', 27),
  ('for-loop-quiz-4', 28),
  ('for-loop-quiz-5', 29),
  ('for-loop-quiz-6', 30),
  ('gym-workout', 31),
  ('analog-reusable-cup-stamps', 32),
  ('beerpong-at-scrollbar', 33),
  ('person-class', 34),
  ('flight-ticket-class', 35),
  ('container-class', 36),
  ('build-a-tree', 37),
  ('grandpas-time-machine', 38),
  ('grandmas-blackmarket-kitchen', 39),
  ('seat-selector', 40)
),
resolved AS (
  SELECT t.id AS assignment_id, o.ord
  FROM ordered o
  JOIN assignment t ON t.slug = o.slug
)
INSERT INTO assignment_set_assignment (assignment_set_id, assignment_id, order_index)
SELECT 'day1-2026', assignment_id, ord         FROM resolved WHERE ord BETWEEN 0 AND 9
UNION ALL
SELECT 'day2-2026', assignment_id, ord - 10    FROM resolved WHERE ord BETWEEN 10 AND 33
UNION ALL
SELECT 'day3-2026', assignment_id, ord - 34    FROM resolved WHERE ord BETWEEN 34 AND 40
UNION ALL
SELECT 'all-assignments-for-solo-2026', assignment_id, ord FROM resolved;

DO $check$
DECLARE n int;
BEGIN
  SELECT count(*) INTO n FROM assignment_set_assignment WHERE assignment_set_id = 'all-assignments-for-solo-2026';
  IF n <> 41 THEN
    RAISE EXCEPTION 'seed error: expected 41 assignments in all-assignments-for-solo-2026, got % (typo in a slug?)', n;
  END IF;
END
$check$;

COMMIT;
