      *===============================================================
      * ex15_calcsal_api.cobol
      * Programme COBOL de calcul de paie — version API
      *
      * Lit depuis stdin : MATRICULE,SALAIRE,ANCIENNETE
      * Ecrit sur stdout : BRUT=xxx|PRIME=xxx|CHARGES=xxx|NET=xxx
      *
      * Compiler dans le Docker :
      *   cobc -x ex15_calcsal_api.cobol -o ex15_calcsal_api
      *===============================================================
       IDENTIFICATION DIVISION.
       PROGRAM-ID. CALCSAL-API.

       ENVIRONMENT DIVISION.

       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-INPUT-LINE    PIC X(50).
       01 WS-MATRICULE     PIC X(6).
       01 WS-SALAIRE-STR   PIC X(12).
       01 WS-ANCIENNETE-STR PIC X(4).
       01 WS-SALAIRE       PIC 9(6)V99.
       01 WS-ANCIENNETE    PIC 9(2).

       01 WS-TAUX-PRIME    PIC 9V99 VALUE 0.
       01 WS-TAUX-CHARGES  PIC 9V99 VALUE 0.22.
       01 WS-PRIME         PIC 9(6)V99 VALUE 0.
       01 WS-BRUT          PIC 9(8)V99 VALUE 0.
       01 WS-CHARGES       PIC 9(8)V99 VALUE 0.
       01 WS-NET           PIC 9(8)V99 VALUE 0.

       01 WS-OUTPUT        PIC X(80).
       01 WS-BRUT-STR      PIC ZZ,ZZ9.99.
       01 WS-PRIME-STR     PIC ZZ,ZZ9.99.
       01 WS-CHARGES-STR   PIC ZZ,ZZ9.99.
       01 WS-NET-STR       PIC ZZ,ZZ9.99.

       PROCEDURE DIVISION.
           ACCEPT WS-INPUT-LINE FROM CONSOLE.

           UNSTRING WS-INPUT-LINE DELIMITED BY ','
               INTO WS-MATRICULE
                    WS-SALAIRE-STR
                    WS-ANCIENNETE-STR.

           MOVE FUNCTION NUMVAL(WS-SALAIRE-STR)    TO WS-SALAIRE.
           MOVE FUNCTION NUMVAL(WS-ANCIENNETE-STR) TO WS-ANCIENNETE.

           COMPUTE WS-TAUX-PRIME = WS-ANCIENNETE * 0.01.
           IF WS-TAUX-PRIME > 0.15
               MOVE 0.15 TO WS-TAUX-PRIME
           END-IF.

           COMPUTE WS-PRIME   = WS-SALAIRE * WS-TAUX-PRIME.
           COMPUTE WS-BRUT    = WS-SALAIRE + WS-PRIME.
           COMPUTE WS-CHARGES = WS-BRUT * WS-TAUX-CHARGES.
           COMPUTE WS-NET     = WS-BRUT - WS-CHARGES.

           MOVE WS-BRUT    TO WS-BRUT-STR.
           MOVE WS-PRIME   TO WS-PRIME-STR.
           MOVE WS-CHARGES TO WS-CHARGES-STR.
           MOVE WS-NET     TO WS-NET-STR.

           STRING 'BRUT='    DELIMITED SIZE WS-BRUT-STR    DELIMITED SIZE
                  '|PRIME='  DELIMITED SIZE WS-PRIME-STR   DELIMITED SIZE
                  '|CHARGES='DELIMITED SIZE WS-CHARGES-STR DELIMITED SIZE
                  '|NET='    DELIMITED SIZE WS-NET-STR     DELIMITED SIZE
                  INTO WS-OUTPUT.

           DISPLAY WS-OUTPUT.
           STOP RUN.
