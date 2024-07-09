import os
import sys

def django_manage():
    os.environ.setdefault("DJANGO_SETTINGS_MODULE", "abr_server.settings")
    from django.core.management import execute_from_command_line
    execute_from_command_line(sys.argv)

if __name__ == '__main__':
    django_manage()